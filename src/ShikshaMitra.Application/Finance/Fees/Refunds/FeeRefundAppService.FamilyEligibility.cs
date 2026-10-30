using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Mobile;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    public virtual async Task<MobileRefundEligibility> FamilyRefundEligibilityAsync(Guid child, Guid year)
    {
        await RequireFamilyRefundAsync(child, year);
        var result = await FamilyEligibilityCoreAsync(child, year);
        var evidence = await RequirePolicyEvidenceAsync(child, year, result.FinanceFeeAccountId, result.StudentFeeStructureId, familyAuthorized: true);
        var policy = evidence.Policy;
        var mapped = MobileFinanceCanonicalCommandProjection.Map(result);
        mapped.FormRules = new MobileRefundFormRules {
            AmountMode = policy.AmountMode.ToString(),
            CanEditLineAmounts = policy.AllowPartialRefund && policy.AmountMode == RefundAmountMode.ManualUpToEligible,
            IncludePaidCharges = policy.IncludePaidCharges, IncludePaidLateFee = policy.IncludePaidLateFee,
            PercentageValue = policy.PercentageValue.HasValue ? MobileFinanceProjection.Money(policy.PercentageValue.Value) : null,
            FixedAmount = policy.FixedAmount.HasValue ? MobileFinanceProjection.Money(policy.FixedAmount.Value) : null
        };
        var dueIds = mapped.FeeHeads.SelectMany(x => x.Months).Where(x => x.DueFeeId.HasValue).Select(x => x.DueFeeId!.Value).ToArray();
        var dues = (await dueFeeRepository.GetListAsync(x => Enumerable.Contains(dueIds, x.Id))).ToDictionary(x => x.Id);
        foreach (var line in mapped.FeeHeads.SelectMany(x => x.Months))
        {
            line.InstallmentLabel = line.DueFeeId.HasValue ? dues[line.DueFeeId.Value].GetDisplayInstallmentLabel() : line.ChargeName ?? string.Empty;
            line.PaymentDate = mapped.Receipts.Single(x => x.ReceiptId == line.SourceReceiptId).ReceiptDate;
        }
        return mapped;
    }

    private async Task<RefundEligibilityDto> FamilyEligibilityCoreAsync(Guid child, Guid academicYearId)
    {
        var assignments = await assignmentRepository.GetListAsync(x => x.StudentId == child && x.AcademicYearId == academicYearId);
        var assignment = assignments.OrderByDescending(x => x.CreationTime).FirstOrDefault()
            ?? throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned);
        if (assignment.FinanceFeeAccountId.HasValue)
        {
            var account = await accountRepository.FindAsync(assignment.FinanceFeeAccountId.Value);
            if (account == null || account.StudentId != child || account.AcademicYearId != academicYearId || account.StudentFeeStructureId != assignment.Id)
                throw new BusinessException(FeeRefundErrorCodes.AssignmentMismatch);
        }
        var payer = await RequirePolicyEvidenceAsync(child, academicYearId, assignment.FinanceFeeAccountId, assignment.Id, familyAuthorized: true);
        // Adopted dues can retain their original, account-less payment evidence.
        // Include those sources only when their assignment policy is the same;
        // never combine money governed by different policy versions in one form.
        var legacyCompatible = !payer.FinanceFeeAccountId.HasValue ||
            assignment.RefundPolicyId == payer.Version.PolicyId && assignment.RefundPolicyVersionId == payer.Version.Id;
        var version = payer.Version;
        var policy = payer.Policy;
        var dues = await dueFeeRepository.GetListAsync(x => x.PaidAmount > 0 && (payer.FinanceFeeAccountId.HasValue
            ? x.FinanceFeeAccountId == payer.FinanceFeeAccountId
            : x.StudentId == payer.StudentId && x.StudentFeeStructureId == payer.AssignmentId));
        if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads)
        {
            var hasPaidDues = dues.Count > 0;
            dues = dues.Where(x => policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)).ToList();
            if (hasPaidDues && dues.Count == 0 && !policy.IncludePaidCharges && !policy.IncludePaidLateFee)
                throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
        }
        var dueIds = dues.Select(x => x.Id).ToList();
        var collections = await collectionRepository.GetListAsync(x =>
            ((payer.FinanceFeeAccountId.HasValue && x.FinanceFeeAccountId == payer.FinanceFeeAccountId) ||
             (legacyCompatible && !x.FinanceFeeAccountId.HasValue && x.StudentId == child)) &&
            x.AcademicYearId == academicYearId && x.Status != FeeCollectionStatus.Reversed &&
            x.Status != FeeCollectionStatus.InReconciliation);
        var collectionIds = collections.Select(x => x.Id).ToList();
        var receipts = await feePaymentReceiptRepository.GetListAsync(x => x.StudentId == child && x.AcademicYearId == academicYearId && collectionIds.Contains(x.FeeCollectionTransactionId) &&
            (x.ReceiptScope == FeeReceiptScope.Fee || x.ReceiptScope == FeeReceiptScope.Combined) && (x.ReceiptKind == FeeReceiptKind.FinalFee ||
            (x.ReceiptKind == FeeReceiptKind.ProvisionalFee && x.VerificationStatus == FeeReceiptVerificationStatus.Verified)));
        var receiptByCollection = receipts.GroupBy(x => x.FeeCollectionTransactionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReceiptKind == FeeReceiptKind.FinalFee).ThenByDescending(x => x.CreationTime).First());
        var eligibleCollectionIds = receiptByCollection.Keys.ToList();
        var allocations = await allocationRepository.GetListAsync(x => eligibleCollectionIds.Contains(x.FeeRecordMetadataId) && dueIds.Contains(x.DueFeeId));
        var allocationIds = allocations.Select(x => x.Id).ToList();
        var committed = await feeRefundRepository.GetCommittedAllocationAmountsAsync(allocationIds);
        var reserved = await feeRefundRepository.GetActiveReservedAmountsAsync(dueIds);
        var dueRemaining = dues.ToDictionary(x => x.Id, x => Math.Max(0m, x.RefundableBalance - reserved.GetValueOrDefault(x.Id)));
        var heads = (await feeHeadRepository.GetListAsync(x => dues.Select(d => d.FeeHeadId).Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
        var dueMap = dues.ToDictionary(x => x.Id);
        var rows = new List<RefundEligibilityMonthDto>();
        foreach (var allocation in allocations.OrderBy(x => receiptByCollection[x.FeeRecordMetadataId].ReceiptDate).ThenBy(x => x.Id))
        {
            var receipt = receiptByCollection[allocation.FeeRecordMetadataId];
            var available = Math.Min(dueRemaining.GetValueOrDefault(allocation.DueFeeId),
                Math.Max(0m, Convert.ToDecimal(allocation.Amount) - committed.GetValueOrDefault(allocation.Id)));
            if (available <= 0) continue;
            dueRemaining[allocation.DueFeeId] -= available;
            var due = dueMap[allocation.DueFeeId];
            rows.Add(new RefundEligibilityMonthDto
            {
                DueFeeId = due.Id, SourceAllocationId = allocation.Id, SourceReceiptId = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber, Month = due.Month, Year = due.Year,
                Total = due.OriginalAmount, Paid = Convert.ToDecimal(allocation.Amount),
                Refunded = committed.GetValueOrDefault(allocation.Id), Refundable = available
            });
        }
        var grouped = rows.GroupBy(x => dueMap[x.DueFeeId!.Value].FeeHeadId).Select(g => new RefundEligibilityFeeHeadDto
        {
            FeeHeadId = g.Key, FeeHeadName = heads.GetValueOrDefault(g.Key, "Fee head"),
            TotalPaid = g.Sum(x => x.Paid), TotalRefunded = g.Sum(x => x.Refunded),
            TotalRefundable = g.Sum(x => x.Refundable), Months = g.ToList()
        }).ToList();
        var chargeSources = policy.IncludePaidCharges
            ? await paidChargeRefundService.GetSourcesAsync(payer.StudentId, academicYearId, payer.FinanceFeeAccountId)
            : [];
        if (policy.IncludePaidCharges && payer.FinanceFeeAccountId.HasValue && legacyCompatible)
            chargeSources.AddRange(await paidChargeRefundService.GetSourcesAsync(child, academicYearId));
        var chargeRows = chargeSources.Where(x => x.Available > 0).Select(x => new RefundEligibilityMonthDto
        {
            ChargeId = x.ChargeId, ChargeName = x.ChargeName, SourceAllocationId = x.AllocationId,
            SourceReceiptId = x.ReceiptId, ReceiptNumber = x.ReceiptNumber,
            Month = (MonthEnum)x.ReceiptDate.Month, Year = x.ReceiptDate.Year,
            Total = x.Total, Paid = x.Paid, Refunded = x.Committed, Refundable = x.Available
        }).ToList();
        if (chargeRows.Count > 0)
            grouped.Add(new RefundEligibilityFeeHeadDto
            {
                FeeHeadName = "Paid charges", TotalPaid = chargeRows.Sum(x => x.Paid),
                TotalRefunded = chargeRows.Sum(x => x.Refunded), TotalRefundable = chargeRows.Sum(x => x.Refundable), Months = chargeRows
            });
        rows.AddRange(chargeRows);
        var lateSources = policy.IncludePaidLateFee
            ? await paidLateFeeRefundService.GetSourcesAsync(payer.StudentId, academicYearId,
                payer.FinanceFeeAccountId, assignmentId: payer.AssignmentId)
            : [];
        if (policy.IncludePaidLateFee && payer.FinanceFeeAccountId.HasValue && legacyCompatible)
            lateSources.AddRange(await paidLateFeeRefundService.GetSourcesAsync(child, academicYearId, assignmentId: payer.AssignmentId));
        var lateRows = lateSources.Where(x => x.Available > 0).Select(x => new RefundEligibilityMonthDto
        {
            LateFeeDueFeeId = x.DueFeeId, ChargeName = x.Name, SourceAllocationId = x.AllocationId,
            SourceReceiptId = x.ReceiptId, ReceiptNumber = x.ReceiptNumber,
            Month = (MonthEnum)x.ReceiptDate.Month, Year = x.ReceiptDate.Year,
            Total = x.Paid, Paid = x.Paid, Refunded = x.Committed, Refundable = x.Available
        }).ToList();
        if (lateRows.Count > 0)
            grouped.Add(new RefundEligibilityFeeHeadDto
            {
                FeeHeadName = "Paid late fees", TotalPaid = lateRows.Sum(x => x.Paid),
                TotalRefunded = lateRows.Sum(x => x.Refunded), TotalRefundable = lateRows.Sum(x => x.Refundable), Months = lateRows
            });
        rows.AddRange(lateRows);
        var totalPaid = allocations.Sum(x => Convert.ToDecimal(x.Amount)) + chargeSources.Sum(x => x.Paid) + lateSources.Sum(x => x.Paid);
        var totalCommitted = allocationIds.Sum(x => committed.GetValueOrDefault(x)) + chargeSources.Sum(x => x.Committed) + lateSources.Sum(x => x.Committed);
        var studentYearCommitted = await GetPayerYearCommittedAsync(payer, academicYearId);
        var refundableFromAllocations = grouped.Sum(x => x.TotalRefundable);
        // Eligibility is a read of the current balance, not a request for a manual amount.
        // The manual calculator deliberately rejects zero requested amounts on write paths.
        var totalRefundable = refundableFromAllocations > 0m
            ? refundPolicyCalculationService.Calculate(policy, new RefundPolicyCalculationInput(
                totalPaid, totalCommitted, 0m, studentYearCommitted,
                policy.AmountMode == RefundAmountMode.ManualUpToEligible ? refundableFromAllocations : null)).FinalRefund
            : 0m;
        return new RefundEligibilityDto
        {
            StudentId = payer.StudentId, FinanceFeeAccountId = payer.FinanceFeeAccountId,
            AcademicYearId = academicYearId, StudentFeeStructureId = payer.AssignmentId,
            RefundPolicyId = version.PolicyId, RefundPolicyVersionId = version.Id, RefundPolicyName = policy.PolicyName,
            AllowedPayoutMethods = policy.AllowedPayoutMethods.Select(ToPayoutMethodLabel).ToList(),
            ProcessingTargetDays = policy.ProcessingTargetDays, AllowPartialRefund = policy.AllowPartialRefund, TotalPaid = totalPaid,
            TotalRefunded = totalCommitted, TotalRefundable = totalRefundable, FeeHeads = grouped,
            Receipts = rows.GroupBy(x => x.SourceReceiptId).Select(g =>
            {
                var receipt = receipts.SingleOrDefault(x => x.Id == g.Key);
                var chargeReceipt = chargeSources.FirstOrDefault(x => x.ReceiptId == g.Key);
                var lateReceipt = lateSources.FirstOrDefault(x => x.ReceiptId == g.Key);
                return new RefundEligibleReceiptDto { ReceiptId = g.Key, ReceiptNumber = receipt?.ReceiptNumber ?? chargeReceipt?.ReceiptNumber ?? lateReceipt!.ReceiptNumber,
                    ReceiptDate = receipt?.ReceiptDate ?? chargeReceipt?.ReceiptDate ?? lateReceipt!.ReceiptDate, PaidAmount = g.Sum(x => x.Paid), RefundableAmount = g.Sum(x => x.Refundable) };
            }).OrderByDescending(x => x.ReceiptDate).ToList()
        };
    }

}
