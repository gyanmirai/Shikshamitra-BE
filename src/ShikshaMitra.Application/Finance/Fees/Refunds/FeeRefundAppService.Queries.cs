using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<PagedResultDto<FeeRefundRequestDto>> GetListAsync(GetFeeRefundsInput input)
    {
        FinanceClassDivisionFilterValidator.Validate(input);
        var scoped = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
        var scopedAccounts = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync();
        var (items, count) = await feeRefundRepository.GetListWithDetailsAsync(input.StudentId, input.AcademicYearId,
            input.AcademicMediumId, input.StandardId, input.DivisionId, input.Status,
            input.LegacyHistory, input.WorkflowVersion, input.Search,
            input.SkipCount, input.MaxResultCount, input.Sorting, scoped,
            scopedAccountIdsQuery: scopedAccounts, financeFeeAccountId: input.FinanceFeeAccountId,
            columnFilters: FeeRefundColumnFilter.Parse(input.Filters));
        var students = await BuildStudentMapAsync(items.Select(x => x.StudentId).Distinct());
        var accounts = await BuildAccountMapAsync(items.Select(x => x.FinanceFeeAccountId));
        var years = await BuildYearMapAsync(items.Select(x => x.AcademicYearId));
        var itemIds = items.Select(x => x.Id).ToList();
        var replacements = itemIds.Count == 0 ? new Dictionary<Guid, Guid>() :
            (await feeRefundRepository.GetListAsync(x =>
                x.ReplacesRefundRequestId.HasValue && itemIds.Contains(x.ReplacesRefundRequestId.Value)))
            .ToDictionary(x => x.ReplacesRefundRequestId!.Value, x => x.Id);
        var dtos = new List<FeeRefundRequestDto>();
        foreach (var item in items)
        {
            var dto = MapToDto(item, students, accounts, years);
            await FilterAvailableActionsAsync(item, dto);
            if (replacements.TryGetValue(item.Id, out var replacementId))
            {
                dto.ReplacedByRefundRequestId = replacementId;
                dto.AvailableActions.Remove("createReplacement");
            }
            dtos.Add(dto);
        }
        return new PagedResultDto<FeeRefundRequestDto>(count, dtos);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<FeeRefundRequestDto> GetAsync(Guid id)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(request.StudentId, request.FinanceFeeAccountId);
        var dto = await MapAsync(request);
        if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.ViewSensitivePaymentDetails))
        {
            dto.PaymentDetails = MapRefundPaymentDetailsToDict(request.RefundPaymentDetails);
            foreach (var settlement in dto.Settlements)
                settlement.ExternalReference = request.Settlements
                    .First(x => x.Id == settlement.Id).ExternalReference;
        }
        await PopulateRefundDetailsAsync(dto, request);
        if (request.WorkflowVersion == 1)
        {
            var postings = await refundAccountingRepository.GetListAsync(x => x.RefundRequestId == id);
            var net = postings.Sum(x => x.IsCorrection ? -x.Amount : x.Amount);
            var lineNet = request.Lines.Sum(x => x.ProcessedAmount - x.ReversedAmount);
            var dueIds = request.Lines.Where(x => x.DueFeeId.HasValue).Select(x => x.DueFeeId!.Value).Distinct().ToList();
            var dues = await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id));
            var allocationIds = request.Lines.Where(x => x.DueFeeId.HasValue && x.SourceAllocationId.HasValue)
                .Select(x => x.SourceAllocationId!.Value).Distinct().ToList();
            var allocations = await allocationRepository.GetListAsync(x => allocationIds.Contains(x.Id));
            var chargeSources = await paidChargeRefundService.GetSourcesAsync(request.StudentId, request.AcademicYearId,
                request.FinanceFeeAccountId, request.OriginalReceiptId, request.Id);
            var chargeEvidenceValid = request.Lines.Where(x => x.ChargeId.HasValue).All(line =>
                chargeSources.Any(source => source.ChargeId == line.ChargeId && source.AllocationId == line.SourceAllocationId &&
                    source.ReceiptId == line.SourceReceiptId && source.Paid >= line.ProcessedAmount - line.ReversedAmount));
            dto.RefundSubledgerNetAmount = net;
            dto.IsRefundSubledgerBalanced = net == request.ProcessedAmount - request.ReversedAmount &&
                net == lineNet && chargeEvidenceValid && postings.Count(x => !x.IsCorrection) <= 1 &&
                postings.Count(x => x.IsCorrection) <= 1 &&
                (request.Status != FeeRefundStatus.Processed || postings.Count(x => !x.IsCorrection) == 1) &&
                dues.Count == dueIds.Count && allocations.Count == allocationIds.Count &&
                request.Lines.Where(line => line.DueFeeId.HasValue).All(line => line.SourceAllocationId.HasValue &&
                    allocations.Any(allocation => allocation.Id == line.SourceAllocationId.Value &&
                        allocation.DueFeeId == line.DueFeeId &&
                        Convert.ToDecimal(allocation.Amount) >= line.ProcessedAmount - line.ReversedAmount) &&
                    dues.Single(due => due.Id == line.DueFeeId).RefundedAmount >=
                    line.ProcessedAmount - line.ReversedAmount);
        }
        dto.ReplacedByRefundRequestId = (await feeRefundRepository
            .GetListAsync(x => x.ReplacesRefundRequestId == request.Id))
            .Select(x => (Guid?)x.Id).FirstOrDefault();
        if (dto.ReplacedByRefundRequestId.HasValue)
            dto.AvailableActions.Remove("createReplacement");
        return dto;
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<RefundEligibilityDto> GetEligibilityAsync(Guid? studentId, Guid academicYearId,
        Guid? financeFeeAccountId = null, Guid? originalReceiptId = null)
    {
        if (originalReceiptId.HasValue)
        {
            var sourceReceipt = await feePaymentReceiptRepository.FindAsync(originalReceiptId.Value)
                ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(sourceReceipt.StudentId, sourceReceipt.FinanceFeeAccountId);
            if (sourceReceipt.AcademicYearId != academicYearId ||
                financeFeeAccountId.HasValue && sourceReceipt.FinanceFeeAccountId != financeFeeAccountId ||
                !sourceReceipt.FinanceFeeAccountId.HasValue && studentId.HasValue && sourceReceipt.StudentId != studentId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            financeFeeAccountId = sourceReceipt.FinanceFeeAccountId;
            studentId ??= sourceReceipt.StudentId;
        }
        var payer = await RequirePolicyEvidenceAsync(studentId, academicYearId, financeFeeAccountId);
        var version = payer.Version;
        var policy = payer.Policy;
        var dues = await dueFeeRepository.GetListAsync(x => x.PaidAmount > 0 && (payer.FinanceFeeAccountId.HasValue
            ? x.FinanceFeeAccountId == payer.FinanceFeeAccountId
            : x.StudentId == payer.StudentId && x.StudentFeeStructureId == payer.AssignmentId));
        if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads)
        {
            var hasPaidDues = dues.Count > 0;
            dues = dues.Where(x => policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)).ToList();
            if (hasPaidDues && dues.Count == 0 && !policy.IncludePaidCharges)
                throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
        }
        var dueIds = dues.Select(x => x.Id).ToList();
        var collections = await collectionRepository.GetListAsync(x => (payer.FinanceFeeAccountId.HasValue
            ? x.FinanceFeeAccountId == payer.FinanceFeeAccountId : x.StudentId == payer.StudentId) &&
            x.AcademicYearId == academicYearId && x.Status != FeeCollectionStatus.Reversed &&
            x.Status != FeeCollectionStatus.InReconciliation);
        var collectionIds = collections.Select(x => x.Id).ToList();
        var receipts = await feePaymentReceiptRepository.GetListAsync(x => collectionIds.Contains(x.FeeCollectionTransactionId) &&
            (!originalReceiptId.HasValue || x.Id == originalReceiptId.Value) &&
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
            ? await paidChargeRefundService.GetSourcesAsync(payer.StudentId, academicYearId, payer.FinanceFeeAccountId, originalReceiptId)
            : [];
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
        var totalPaid = allocations.Sum(x => Convert.ToDecimal(x.Amount)) + chargeSources.Sum(x => x.Paid);
        var totalCommitted = allocationIds.Sum(x => committed.GetValueOrDefault(x)) + chargeSources.Sum(x => x.Committed);
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
                return new RefundEligibleReceiptDto { ReceiptId = g.Key, ReceiptNumber = receipt?.ReceiptNumber ?? chargeReceipt!.ReceiptNumber,
                    ReceiptDate = receipt?.ReceiptDate ?? chargeReceipt!.ReceiptDate, PaidAmount = g.Sum(x => x.Paid), RefundableAmount = g.Sum(x => x.Refundable) };
            }).OrderByDescending(x => x.ReceiptDate).ToList()
        };
    }

    private static string ToPayoutMethodLabel(RefundPayoutMethod method) => method switch
    {
        RefundPayoutMethod.Cash => "Cash", RefundPayoutMethod.Cheque => "Cheque",
        RefundPayoutMethod.DemandDraft => "Demand Draft (DD)",
        RefundPayoutMethod.BankTransfer => "Bank Transfer (NEFT/RTGS/IMPS)",
        RefundPayoutMethod.Upi => "UPI", _ => method.ToString()
    };
}
