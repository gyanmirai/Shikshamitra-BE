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
    private sealed record PreparedRefund(Guid? AssignmentId, Guid PolicyId, Guid PolicyVersionId,
        FeePaymentReceipt Receipt, RefundPolicySnapshot Policy, List<RefundLineSpec> Lines,
        RefundPolicyCalculationResult Calculation, string PolicyJson, string CalculationJson, string Hash,
        Guid? StudentId, Guid? FinanceFeeAccountId);

    private sealed record RefundPolicyEvidence(Guid? StudentId, Guid? FinanceFeeAccountId,
        Guid? AssignmentId, FinancePolicyVersion Version, RefundPolicySnapshot Policy);

    private async Task<RefundPolicyEvidence> RequirePolicyEvidenceAsync(Guid? studentId,
        Guid academicYearId, Guid? accountId, Guid? assignmentId = null)
    {
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(studentId, accountId);
        Guid policyId;
        Guid versionId;
        if (accountId.HasValue)
        {
            var account = await accountRepository.GetAsync(accountId.Value);
            if (account.AcademicYearId != academicYearId || studentId.HasValue && account.StudentId != studentId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            var reference = account.GetPolicyReference("refund")
                ?? throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned);
            policyId = reference.PolicyId;
            versionId = reference.VersionId;
            studentId = account.StudentId;
            assignmentId = account.StudentFeeStructureId;
        }
        else
        {
            if (!studentId.HasValue) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            var assignments = await assignmentRepository.GetListAsync(x => x.StudentId == studentId && x.AcademicYearId == academicYearId);
            var assignment = assignmentId.HasValue
                ? assignments.SingleOrDefault(x => x.Id == assignmentId.Value)
                : assignments.OrderByDescending(x => x.CreationTime).FirstOrDefault();
            if (assignment == null || !assignment.RefundPolicyId.HasValue || !assignment.RefundPolicyVersionId.HasValue)
                throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned);
            assignmentId = assignment.Id;
            policyId = assignment.RefundPolicyId.Value;
            versionId = assignment.RefundPolicyVersionId.Value;
        }
        var version = await policyVersionRepository.FindAsync(versionId);
        if (version == null || version.PolicyType != "refund" || version.PolicyId != policyId || version.AcademicYearId != academicYearId)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        RefundPolicySnapshot policy;
        try { policy = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson)!; }
        catch (JsonException) { throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid); }
        if (policy == null || policy.PolicyId != policyId)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        return new RefundPolicyEvidence(studentId, accountId, assignmentId, version, policy);
    }

    private Task<decimal> GetPayerYearCommittedAsync(RefundPolicyEvidence payer, Guid yearId, Guid? excludingId = null) =>
        payer.StudentId.HasValue
            ? feeRefundRepository.GetStudentYearCommittedAmountAsync(payer.StudentId.Value, yearId, excludingId)
            : feeRefundRepository.GetAccountYearCommittedAmountAsync(payer.FinanceFeeAccountId!.Value, yearId, excludingId);

    private async Task<PreparedRefund> PrepareAsync(CreateFeeRefundRequestDto input, Guid? excludingRequestId = null)
    {
        if (input.Lines.Count == 0 || input.Lines.Any(x => x.Amount <= 0 || x.DueFeeId.HasValue == x.ChargeId.HasValue) ||
            input.Lines.Select(x => x.SourceAllocationId).Distinct().Count() != input.Lines.Count)
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        var receipt = await feePaymentReceiptRepository.FindAsync(input.OriginalReceiptId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(receipt.StudentId, receipt.FinanceFeeAccountId);
        if (input.FinanceFeeAccountId.HasValue && receipt.FinanceFeeAccountId != input.FinanceFeeAccountId ||
            !receipt.FinanceFeeAccountId.HasValue && receipt.StudentId != input.StudentId ||
            receipt.AcademicYearId != input.AcademicYearId ||
            receipt.ReceiptScope is not (FeeReceiptScope.Fee or FeeReceiptScope.Combined) ||
            (receipt.ReceiptKind != FeeReceiptKind.FinalFee &&
             !(receipt.ReceiptKind == FeeReceiptKind.ProvisionalFee && receipt.VerificationStatus == FeeReceiptVerificationStatus.Verified)))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);

        var collection = await collectionRepository.FindAsync(receipt.FeeCollectionTransactionId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (collection.FinanceFeeAccountId != receipt.FinanceFeeAccountId ||
            collection.AcademicYearId != receipt.AcademicYearId ||
            !receipt.FinanceFeeAccountId.HasValue && collection.StudentId != receipt.StudentId)
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (collection.Status is FeeCollectionStatus.Reversed or FeeCollectionStatus.InReconciliation)
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);

        var allocationIds = input.Lines.Where(x => x.DueFeeId.HasValue).Select(x => x.SourceAllocationId).Distinct().ToList();
        var allocations = await allocationRepository.GetListAsync(x => allocationIds.Contains(x.Id));
        if (allocations.Count != allocationIds.Count || allocations.Any(x => x.FeeRecordMetadataId != collection.Id))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        var allocationMap = allocations.ToDictionary(x => x.Id);

        var dueIds = input.Lines.Where(x => x.DueFeeId.HasValue).Select(x => x.DueFeeId!.Value).Distinct().ToList();
        var dues = await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id));
        if (dues.Count != dueIds.Count || dues.Any(x => receipt.FinanceFeeAccountId.HasValue
            ? x.FinanceFeeAccountId != receipt.FinanceFeeAccountId
            : x.StudentId != input.StudentId))
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        if (!receipt.FinanceFeeAccountId.HasValue && (dues.Any(x => !x.StudentFeeStructureId.HasValue) || dues.Select(x => x.StudentFeeStructureId).Distinct().Count() > 1))
            throw new BusinessException(FeeRefundErrorCodes.AssignmentMismatch);
        var payer = await RequirePolicyEvidenceAsync(input.StudentId, input.AcademicYearId,
            receipt.FinanceFeeAccountId, receipt.FinanceFeeAccountId.HasValue || dues.Count == 0 ? null : dues[0].StudentFeeStructureId);
        if (!receipt.FinanceFeeAccountId.HasValue)
        {
            // Adoption binds the existing dues to an account; historical receipts and
            // collection allocations retain their original real-Student identity.
            var adoptedAccountIds = dues.Where(x => x.FinanceFeeAccountId.HasValue)
                .Select(x => x.FinanceFeeAccountId!.Value).Distinct().ToList();
            if (adoptedAccountIds.Count > 0)
            {
                var accounts = await accountRepository.GetListAsync(x => adoptedAccountIds.Contains(x.Id));
                if (accounts.Count != adoptedAccountIds.Count || accounts.Any(x =>
                    x.StudentId != payer.StudentId || x.AcademicYearId != input.AcademicYearId ||
                    x.StudentFeeStructureId != payer.AssignmentId))
                    throw new BusinessException(FeeRefundErrorCodes.AssignmentMismatch);
            }
        }
        var version = payer.Version;
        var policy = payer.Policy;
        ValidatePolicyContext(policy, receipt, collection, input.RefundMethod, dues);

        var feeHeadIds = dues.Select(x => x.FeeHeadId).Distinct().ToList();
        var heads = (await feeHeadRepository.GetListAsync(x => feeHeadIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
        var allocationCommitted = await feeRefundRepository.GetCommittedAllocationAmountsAsync(allocationIds, excludingRequestId);
        var dueReserved = await feeRefundRepository.GetActiveReservedAmountsAsync(dueIds, excludingRequestId);
        var dueMap = dues.ToDictionary(x => x.Id);
        var dueRemaining = dues.ToDictionary(x => x.Id,
            x => Math.Max(0m, x.RefundableBalance - dueReserved.GetValueOrDefault(x.Id)));
        var specs = new List<RefundLineSpec>();
        foreach (var item in input.Lines.Where(x => x.DueFeeId.HasValue))
        {
            var allocation = allocationMap[item.SourceAllocationId];
            if (allocation.DueFeeId != item.DueFeeId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            var due = dueMap[item.DueFeeId!.Value];
            var allocationAvailable = Math.Max(0m, Convert.ToDecimal(allocation.Amount) - allocationCommitted.GetValueOrDefault(allocation.Id));
            var available = Math.Min(allocationAvailable, dueRemaining[due.Id]);
            if (item.Amount > available)
                throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance)
                    .WithData("DueFeeId", due.Id).WithData("Requested", item.Amount).WithData("Available", available);
            var requestedAmount = policy.AmountMode == RefundAmountMode.ManualUpToEligible ? item.Amount : available;
            dueRemaining[due.Id] -= requestedAmount;
            specs.Add(new RefundLineSpec(due.Id, allocation.Id, receipt.Id, due.FeeHeadId,
                heads.GetValueOrDefault(due.FeeHeadId, allocation.FeeHeadName ?? "Fee head"),
                due.GetDisplayInstallmentLabel(), available, requestedAmount, 0m));
        }

        var chargeLines = input.Lines.Where(x => x.ChargeId.HasValue).ToList();
        if (chargeLines.Count > 0)
        {
            if (!policy.IncludePaidCharges) throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
            var sources = await paidChargeRefundService.GetSourcesAsync(payer.StudentId, input.AcademicYearId,
                payer.FinanceFeeAccountId, receipt.Id, excludingRequestId);
            foreach (var item in chargeLines)
            {
                var source = sources.SingleOrDefault(x => x.ChargeId == item.ChargeId && x.AllocationId == item.SourceAllocationId);
                if (source == null) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
                if (item.Amount > source.Available) throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
                var amount = policy.AmountMode == RefundAmountMode.ManualUpToEligible ? item.Amount : source.Available;
                specs.Add(new RefundLineSpec(null, source.AllocationId, receipt.Id, null, "Paid charges",
                    source.ChargeName, source.Available, amount, 0m, source.ChargeId));
            }
        }
        var requested = specs.Sum(x => x.Amount);
        var policyBase = specs.Sum(x => x.SettledPaidAmount);
        if (!policy.AllowPartialRefund)
            await EnsureWholeReceiptSelectedAsync(collection.Id, payer.AssignmentId, policy, input.Lines.Select(x => x.SourceAllocationId).ToList(), excludingRequestId, payer.FinanceFeeAccountId, payer.StudentId, input.AcademicYearId, receipt.Id);
        var studentYearCommitted = await GetPayerYearCommittedAsync(payer, input.AcademicYearId, excludingRequestId);
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
        return new PreparedRefund(payer.AssignmentId, version.PolicyId, version.Id, receipt, policy,
            specs, calculation, version.SnapshotJson, calculationJson, hash, payer.StudentId, payer.FinanceFeeAccountId);
    }

    private async Task EnsureWholeReceiptSelectedAsync(Guid collectionId, Guid? assignmentId,
        RefundPolicySnapshot policy, IReadOnlyCollection<Guid> selectedAllocationIds, Guid? excludingRequestId,
        Guid? accountId = null, Guid? studentId = null, Guid? academicYearId = null, Guid? receiptId = null)
    {
        var eligibleDues = await dueFeeRepository.GetListAsync(x => x.PaidAmount > 0 && (accountId.HasValue
            ? x.FinanceFeeAccountId == accountId : x.StudentFeeStructureId == assignmentId));
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
        if (policy.IncludePaidCharges)
        {
            var sources = await paidChargeRefundService.GetSourcesAsync(studentId, academicYearId!.Value,
                accountId, receiptId, excludingRequestId);
            expected.UnionWith(sources.Where(x => x.Available > 0).Select(x => x.AllocationId));
        }
        if (!expected.SetEquals(selectedAllocationIds))
            throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount)
                .WithData("Reason", "All currently eligible allocations on the receipt must be selected.");
    }

    private static List<RefundLineSpec> AllocateCalculatedAmounts(
        IReadOnlyCollection<RefundLineSpec> source, decimal finalAmount)
    {
        // Preserve source precision for historic fractional receipt allocations.
        // New whole-rupee sources allocate whole rupees, with no lost remainder.
        var factor = source.Any(x => x.Amount != decimal.Truncate(x.Amount)) ? 100m : 1m;
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
            "DemandDraft" or "Demand Draft (DD)" => RefundPayoutMethod.DemandDraft,
            "BankTransfer" or "Bank Transfer (NEFT/RTGS/IMPS)" => RefundPayoutMethod.BankTransfer,
            "Upi" or "UPI" => RefundPayoutMethod.Upi,
            _ => (RefundPayoutMethod)(-1)
        };
        return (int)method >= 0;
    }
}
