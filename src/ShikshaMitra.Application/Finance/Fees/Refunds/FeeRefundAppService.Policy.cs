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
        Guid academicYearId, Guid? accountId, Guid? assignmentId = null, bool familyAuthorized = false)
    {
        if (!familyAuthorized) await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(studentId, accountId);
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

    private async Task EnsureApprovalWithinAnnualCapAsync(FeeRefundRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PolicySnapshotJson)) return;
        var policy = JsonSerializer.Deserialize<RefundPolicySnapshot>(request.PolicySnapshotJson)
            ?? throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        if (!policy.MaximumPerStudentYear.HasValue) return;
        var committed = request.StudentId.HasValue
            ? await feeRefundRepository.GetStudentYearCommittedAmountAsync(request.StudentId.Value, request.AcademicYearId, request.Id)
            : await feeRefundRepository.GetAccountYearCommittedAmountAsync(request.FinanceFeeAccountId!.Value, request.AcademicYearId, request.Id);
        var remaining = Math.Max(0m, policy.MaximumPerStudentYear.Value - committed);
        if (request.CalculatedAmount > remaining)
            throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount)
                .WithData("Requested", request.CalculatedAmount).WithData("Allowed", remaining);
    }

    private async Task<PreparedRefund> PrepareAsync(CreateFeeRefundRequestDto input, Guid? excludingRequestId = null, bool familyAuthorized = false)
    {
        if (input.Lines.Count == 0 || input.Lines.Any(x => x.Amount <= 0 || (x.DueFeeId.HasValue ? 1 : 0) + (x.ChargeId.HasValue ? 1 : 0) + (x.LateFeeDueFeeId.HasValue ? 1 : 0) != 1) ||
            input.Lines.Select(x => x.SourceAllocationId).Distinct().Count() != input.Lines.Count)
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        var receipt = await feePaymentReceiptRepository.FindAsync(input.OriginalReceiptId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (!familyAuthorized) await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(receipt.StudentId, receipt.FinanceFeeAccountId);
        if (familyAuthorized && receipt.StudentId != input.StudentId) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (input.FinanceFeeAccountId.HasValue && receipt.FinanceFeeAccountId != input.FinanceFeeAccountId ||
            !receipt.FinanceFeeAccountId.HasValue && receipt.StudentId != input.StudentId ||
            receipt.AcademicYearId != input.AcademicYearId ||
            !RefundReceiptEvidencePolicy.IsSettled(receipt) ||
            input.Lines.Any(x => x.DueFeeId.HasValue && (x.SourceReceiptId ?? input.OriginalReceiptId) == receipt.Id) && !RefundReceiptEvidencePolicy.CoversPrincipal(receipt))
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);

        var collection = await collectionRepository.FindAsync(receipt.FeeCollectionTransactionId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (collection.FinanceFeeAccountId != receipt.FinanceFeeAccountId ||
            collection.AcademicYearId != receipt.AcademicYearId ||
            !receipt.FinanceFeeAccountId.HasValue && collection.StudentId != receipt.StudentId)
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        if (collection.Status is FeeCollectionStatus.Reversed or FeeCollectionStatus.InReconciliation)
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);

        var receiptIds = input.Lines.Select(x => x.SourceReceiptId ?? input.OriginalReceiptId)
            .Append(input.OriginalReceiptId).Distinct().ToArray();
        var receipts = (await feePaymentReceiptRepository.GetListAsync(x => Enumerable.Contains(receiptIds, x.Id)))
            .ToDictionary(x => x.Id);
        if (receipts.Count != receiptIds.Length)
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        var collectionIds = receipts.Values.Select(x => x.FeeCollectionTransactionId).Distinct().ToArray();
        var collections = (await collectionRepository.GetListAsync(x => Enumerable.Contains(collectionIds, x.Id)))
            .ToDictionary(x => x.Id);
        if (collections.Count != collectionIds.Length)
            throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
        foreach (var source in receipts.Values)
        {
            if (source.FinanceFeeAccountId != receipt.FinanceFeeAccountId ||
                !receipt.FinanceFeeAccountId.HasValue && source.StudentId != receipt.StudentId ||
                receipt.FinanceFeeAccountId.HasValue && source.StudentId.HasValue && input.StudentId.HasValue && source.StudentId != input.StudentId ||
                source.AcademicYearId != input.AcademicYearId || !RefundReceiptEvidencePolicy.IsSettled(source) ||
                input.Lines.Any(x => x.DueFeeId.HasValue && (x.SourceReceiptId ?? input.OriginalReceiptId) == source.Id) &&
                !RefundReceiptEvidencePolicy.CoversPrincipal(source))
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            var payment = collections[source.FeeCollectionTransactionId];
            if (payment.FinanceFeeAccountId != source.FinanceFeeAccountId || payment.AcademicYearId != source.AcademicYearId ||
                !source.FinanceFeeAccountId.HasValue && payment.StudentId != source.StudentId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            if (payment.Status is FeeCollectionStatus.Reversed or FeeCollectionStatus.InReconciliation)
                throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        }

        var allocationIds = input.Lines.Where(x => x.DueFeeId.HasValue).Select(x => x.SourceAllocationId).Distinct().ToList();
        var allocations = await allocationRepository.GetListAsync(x => allocationIds.Contains(x.Id));
        if (allocations.Count != allocationIds.Count || allocations.Any(x => !collections.ContainsKey(x.FeeRecordMetadataId)))
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
            receipt.FinanceFeeAccountId, receipt.FinanceFeeAccountId.HasValue || dues.Count == 0 ? null : dues[0].StudentFeeStructureId, familyAuthorized);
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
            var sourceReceipt = receipts[item.SourceReceiptId ?? input.OriginalReceiptId];
            if (allocation.DueFeeId != item.DueFeeId || allocation.FeeRecordMetadataId != sourceReceipt.FeeCollectionTransactionId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            var due = dueMap[item.DueFeeId!.Value];
            var allocationAvailable = Math.Max(0m, Convert.ToDecimal(allocation.Amount) - allocationCommitted.GetValueOrDefault(allocation.Id));
            var available = Math.Min(allocationAvailable, dueRemaining[due.Id]);
            if (item.Amount > available)
                throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance)
                    .WithData("DueFeeId", due.Id).WithData("Requested", item.Amount).WithData("Available", available);
            var requestedAmount = item.Amount;
            dueRemaining[due.Id] -= requestedAmount;
            specs.Add(new RefundLineSpec(due.Id, allocation.Id, sourceReceipt.Id, due.FeeHeadId,
                heads.GetValueOrDefault(due.FeeHeadId, allocation.FeeHeadName ?? "Fee head"),
                due.GetDisplayInstallmentLabel(), available, requestedAmount, 0m));
        }

        var chargeLines = input.Lines.Where(x => x.ChargeId.HasValue).ToList();
        if (chargeLines.Count > 0)
        {
            if (!policy.IncludePaidCharges) throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
            var sources = await paidChargeRefundService.GetSourcesAsync(payer.StudentId, input.AcademicYearId,
                payer.FinanceFeeAccountId, null, excludingRequestId);
            foreach (var item in chargeLines)
            {
                var source = sources.SingleOrDefault(x => x.ChargeId == item.ChargeId && x.AllocationId == item.SourceAllocationId &&
                    x.ReceiptId == (item.SourceReceiptId ?? input.OriginalReceiptId));
                if (source == null) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
                if (item.Amount > source.Available) throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
                var amount = item.Amount;
                specs.Add(new RefundLineSpec(null, source.AllocationId, source.ReceiptId, null, "Paid charges",
                    source.ChargeName, source.Available, amount, 0m, source.ChargeId));
            }
        }
        var lateLines = input.Lines.Where(x => x.LateFeeDueFeeId.HasValue).ToList();
        if (lateLines.Count > 0)
        {
            if (!policy.IncludePaidLateFee) throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
            var sources = await paidLateFeeRefundService.GetSourcesAsync(payer.StudentId, input.AcademicYearId,
                payer.FinanceFeeAccountId, null, excludingRequestId, payer.AssignmentId);
            foreach (var item in lateLines)
            {
                var source = sources.SingleOrDefault(x => x.DueFeeId == item.LateFeeDueFeeId && x.AllocationId == item.SourceAllocationId &&
                    x.ReceiptId == (item.SourceReceiptId ?? input.OriginalReceiptId));
                if (source == null) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
                if (item.Amount > source.Available) throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
                var amount = item.Amount;
                specs.Add(new RefundLineSpec(null, source.AllocationId, source.ReceiptId, null, "Paid late fees",
                    source.Name, source.Available, amount, 0m, LateFeeDueFeeId: source.DueFeeId));
            }
        }
        var requested = specs.Sum(x => x.Amount);
        var policyBase = specs.Sum(x => x.SettledPaidAmount);
        var studentYearCommitted = await GetPayerYearCommittedAsync(payer, input.AcademicYearId, excludingRequestId);
        var calculation = refundPolicyCalculationService.Calculate(policy, new RefundPolicyCalculationInput(
            policyBase, 0m, 0m, studentYearCommitted,
            requested));
        if (calculation.FinalRefund <= 0 || calculation.FinalRefund > requested)
            throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount)
                .WithData("Requested", requested).WithData("Allowed", calculation.FinalRefund);
        specs = AllocateCalculatedAmounts(specs, calculation.FinalRefund);
        var calculationJson = JsonSerializer.Serialize(new
        {
            SourceReceiptId = receipt.Id,
            SourceReceiptNumber = receipt.ReceiptNumber,
            SourceReceipts = receipts.Values.OrderBy(x => x.Id).Select(x => new { ReceiptId = x.Id, x.ReceiptNumber }),
            PolicyVersionId = version.Id,
            RequestedAmount = requested,
            Result = calculation,
            Lines = specs.OrderBy(x => x.SourceAllocationId)
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(calculationJson))).ToLowerInvariant();
        return new PreparedRefund(payer.AssignmentId, version.PolicyId, version.Id, receipt, policy,
            specs, calculation, version.SnapshotJson, calculationJson, hash, payer.StudentId, payer.FinanceFeeAccountId);
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
            _ => (RefundPayoutMethod) (-1)
        };
        return (int) method >= 0;
    }
}
