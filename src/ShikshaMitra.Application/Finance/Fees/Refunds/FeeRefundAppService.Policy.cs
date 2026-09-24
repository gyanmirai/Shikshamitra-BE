using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    private sealed record PreparedRefund(Guid AssignmentId, Guid PolicyId, Guid PolicyVersionId,
        FeePaymentReceipt Receipt, RefundPolicySnapshot Policy, List<RefundLineSpec> Lines,
        RefundPolicyCalculationResult Calculation, string PolicyJson, string CalculationJson, string Hash);

    private async Task<PreparedRefund> PrepareAsync(CreateFeeRefundRequestDto input, Guid? excludingRequestId = null)
    {
        await financeScopeAccessService.EnsureStudentAccessibleAsync(input.StudentId);
        var receipt = await feePaymentReceiptRepository.FindAsync(input.OriginalReceiptId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (receipt.StudentId != input.StudentId || receipt.AcademicYearId != input.AcademicYearId ||
            receipt.ReceiptScope != FeeReceiptScope.Fee ||
            (receipt.ReceiptKind != FeeReceiptKind.FinalFee &&
             !(receipt.ReceiptKind == FeeReceiptKind.ProvisionalFee && receipt.VerificationStatus == FeeReceiptVerificationStatus.Verified)))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);

        var collection = await collectionRepository.FindAsync(receipt.FeeCollectionTransactionId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (collection.Status is FeeCollectionStatus.Reversed or FeeCollectionStatus.InReconciliation)
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);

        var allocationIds = input.Lines.Select(x => x.SourceAllocationId).Distinct().ToList();
        var allocations = await allocationRepository.GetListAsync(x => allocationIds.Contains(x.Id));
        if (allocations.Count != allocationIds.Count || allocations.Any(x => x.FeeRecordMetadataId != collection.Id))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        var allocationMap = allocations.ToDictionary(x => x.Id);

        var dueIds = input.Lines.Select(x => x.DueFeeId).Distinct().ToList();
        var dues = await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id));
        if (dues.Count != dueIds.Count || dues.Any(x => x.StudentId != input.StudentId))
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        if (dues.Any(x => !x.StudentFeeStructureId.HasValue) || dues.Select(x => x.StudentFeeStructureId).Distinct().Count() != 1)
            throw new BusinessException(FeeRefundErrorCodes.AssignmentMismatch);
        var assignmentId = dues[0].StudentFeeStructureId!.Value;
        var assignment = await assignmentRepository.FindAsync(assignmentId)
            ?? throw new BusinessException(FeeRefundErrorCodes.AssignmentMismatch);
        if (assignment.StudentId != input.StudentId || assignment.AcademicYearId != input.AcademicYearId ||
            !assignment.RefundPolicyId.HasValue || !assignment.RefundPolicyVersionId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned);

        var version = await policyVersionRepository.FindAsync(assignment.RefundPolicyVersionId.Value);
        if (version == null || version.PolicyType != "refund" || version.PolicyId != assignment.RefundPolicyId ||
            version.AcademicYearId != input.AcademicYearId)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        RefundPolicySnapshot policy;
        try { policy = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson)!; }
        catch (JsonException) { throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid); }
        if (policy == null || policy.PolicyId != version.PolicyId)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        ValidatePolicyContext(policy, receipt, collection, input.RefundMethod, dues);

        var feeHeadIds = dues.Select(x => x.FeeHeadId).Distinct().ToList();
        var heads = (await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
        var allocationCommitted = await feeRefundRepository.GetCommittedAllocationAmountsAsync(allocationIds, excludingRequestId);
        var dueReserved = await feeRefundRepository.GetActiveReservedAmountsAsync(dueIds, excludingRequestId);
        var dueMap = dues.ToDictionary(x => x.Id);
        var specs = new List<RefundLineSpec>();
        foreach (var item in input.Lines)
        {
            var allocation = allocationMap[item.SourceAllocationId];
            if (allocation.DueFeeId != item.DueFeeId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            var due = dueMap[item.DueFeeId];
            var allocationAvailable = Math.Max(0m, Convert.ToDecimal(allocation.Amount) - allocationCommitted.GetValueOrDefault(allocation.Id));
            var dueAvailable = Math.Max(0m, due.RefundableBalance - dueReserved.GetValueOrDefault(due.Id));
            var available = Math.Min(allocationAvailable, dueAvailable);
            if (item.Amount > available)
                throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance)
                    .WithData("DueFeeId", due.Id).WithData("Requested", item.Amount).WithData("Available", available);
            var requestedAmount = policy.AmountMode == RefundAmountMode.ManualUpToEligible ? item.Amount : available;
            specs.Add(new RefundLineSpec(due.Id, allocation.Id, receipt.Id, due.FeeHeadId,
                heads.GetValueOrDefault(due.FeeHeadId, allocation.FeeHeadName ?? "Fee head"),
                due.GetDisplayInstallmentLabel(), available, requestedAmount, 0m));
        }

        var requested = specs.Sum(x => x.Amount);
        var policyBase = specs.Sum(x => x.SettledPaidAmount);
        if (!policy.AllowPartialRefund)
            await EnsureWholeReceiptSelectedAsync(collection.Id, assignment.Id, policy, allocationIds, excludingRequestId);
        var studentYearCommitted = await feeRefundRepository.GetStudentYearCommittedAmountAsync(input.StudentId, input.AcademicYearId, excludingRequestId);
        var calculation = refundPolicyCalculationService.Calculate(policy, new RefundPolicyCalculationInput(
            policyBase, 0m, 0m, studentYearCommitted,
            policy.AmountMode == RefundAmountMode.ManualUpToEligible ? requested : null));
        if (calculation.FinalRefund <= 0 || calculation.FinalRefund > requested)
            throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount)
                .WithData("Requested", requested).WithData("Allowed", calculation.FinalRefund);
        specs = AllocateCalculatedAmounts(specs, calculation.FinalRefund);
        var calculationJson = JsonSerializer.Serialize(new
        {
            SourceReceiptId = receipt.Id, SourceReceiptNumber = receipt.ReceiptNumber,
            PolicyVersionId = version.Id, RequestedAmount = requested, Result = calculation,
            Lines = specs.OrderBy(x => x.SourceAllocationId)
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(calculationJson))).ToLowerInvariant();
        return new PreparedRefund(assignment.Id, version.PolicyId, version.Id, receipt, policy,
            specs, calculation, version.SnapshotJson, calculationJson, hash);
    }

    private async Task EnsureWholeReceiptSelectedAsync(Guid collectionId, Guid assignmentId,
        RefundPolicySnapshot policy, IReadOnlyCollection<Guid> selectedAllocationIds, Guid? excludingRequestId)
    {
        var eligibleDues = await dueFeeRepository.GetListAsync(x => x.StudentFeeStructureId == assignmentId && x.PaidAmount > 0);
        if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads)
            eligibleDues = eligibleDues.Where(x => policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)).ToList();
        var eligibleDueIds = eligibleDues.Select(x => x.Id).ToList();
        var receiptAllocations = await allocationRepository.GetListAsync(x =>
            x.FeeRecordMetadataId == collectionId && eligibleDueIds.Contains(x.DueFeeId));
        var receiptAllocationIds = receiptAllocations.Select(x => x.Id).ToList();
        var committed = await feeRefundRepository.GetCommittedAllocationAmountsAsync(receiptAllocationIds, excludingRequestId);
        var reserved = await feeRefundRepository.GetActiveReservedAmountsAsync(eligibleDueIds, excludingRequestId);
        var dueRemaining = eligibleDues.ToDictionary(x => x.Id,
            x => Math.Max(0m, x.RefundableBalance - reserved.GetValueOrDefault(x.Id)));
        var expected = new HashSet<Guid>();
        foreach (var allocation in receiptAllocations.OrderBy(x => x.Id))
        {
            var available = Math.Min(dueRemaining.GetValueOrDefault(allocation.DueFeeId),
                Math.Max(0m, Convert.ToDecimal(allocation.Amount) - committed.GetValueOrDefault(allocation.Id)));
            if (available <= 0) continue;
            expected.Add(allocation.Id);
            dueRemaining[allocation.DueFeeId] -= available;
        }
        if (!expected.SetEquals(selectedAllocationIds))
            throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount)
                .WithData("Reason", "All currently eligible allocations on the receipt must be selected.");
    }

    private static List<RefundLineSpec> AllocateCalculatedAmounts(
        IReadOnlyCollection<RefundLineSpec> source, decimal finalAmount)
    {
        const decimal factor = 100m;
        var targetUnits = decimal.ToInt64(decimal.Round(finalAmount * factor, 0, MidpointRounding.AwayFromZero));
        var totalWeight = source.Sum(x => x.Amount);
        if (targetUnits <= 0 || totalWeight <= 0)
            throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount);

        var rows = source.Select(x =>
        {
            var exactUnits = targetUnits * x.Amount / totalWeight;
            var floorUnits = decimal.ToInt64(decimal.Floor(exactUnits));
            var capUnits = decimal.ToInt64(decimal.Floor(x.Amount * factor));
            return new { Line = x, Units = Math.Min(floorUnits, capUnits), Cap = capUnits, Remainder = exactUnits - floorUnits };
        }).OrderByDescending(x => x.Remainder)
          .ThenBy(x => x.Line.SourceAllocationId)
          .ToList();

        var allocated = rows.Sum(x => x.Units);
        var mutable = rows.Select(x => (x.Line, x.Units, x.Cap, x.Remainder)).ToList();
        while (allocated < targetUnits)
        {
            var changed = false;
            for (var i = 0; i < mutable.Count && allocated < targetUnits; i++)
            {
                var row = mutable[i];
                if (row.Units >= row.Cap) continue;
                mutable[i] = (row.Line, row.Units + 1, row.Cap, row.Remainder);
                allocated++;
                changed = true;
            }
            if (!changed)
                throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount)
                    .WithData("Reason", "The policy amount cannot be allocated to the selected lines.");
        }

        return mutable.Select(x => x.Line with { CalculatedAmount = x.Units / factor }).ToList();
    }

    private void ValidatePolicyContext(RefundPolicySnapshot policy, FeePaymentReceipt receipt,
        FeeRecordMetadata collection, string? method, IReadOnlyCollection<DueFee> dues)
    {
        if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads && dues.Any(x => !policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)))
            throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
        if (!TryMapPayoutMethod(method, out var payout) || !policy.AllowedPayoutMethods.Contains(payout))
            throw new BusinessException(FeeRefundErrorCodes.PayoutMethodNotAllowed);
    }

    private static bool TryMapPayoutMethod(string? value, out RefundPayoutMethod method)
    {
        method = value switch
        {
            "Cash" => RefundPayoutMethod.Cash,
            "Cheque" => RefundPayoutMethod.Cheque,
            "Demand Draft (DD)" => RefundPayoutMethod.DemandDraft,
            "Bank Transfer (NEFT/RTGS/IMPS)" => RefundPayoutMethod.BankTransfer,
            "UPI" => RefundPayoutMethod.Upi,
            _ => (RefundPayoutMethod)(-1)
        };
        return (int)method >= 0;
    }
}
