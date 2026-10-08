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
        var (items, count) = await feeRefundRepository.GetListWithDetailsAsync(input.StudentId, input.AcademicYearId,
            input.AcademicMediumId, input.StandardId, input.DivisionId, input.Status,
            input.LegacyHistory, input.WorkflowVersion, input.Search,
            input.SkipCount, input.MaxResultCount, input.Sorting, scoped);
        var students = await BuildStudentMapAsync(items.Select(x => x.StudentId).Distinct());
        var itemIds = items.Select(x => x.Id).ToList();
        var replacements = itemIds.Count == 0 ? new Dictionary<Guid, Guid>() :
            (await feeRefundRepository.GetListAsync(x =>
                x.ReplacesRefundRequestId.HasValue && itemIds.Contains(x.ReplacesRefundRequestId.Value)))
            .ToDictionary(x => x.ReplacesRefundRequestId!.Value, x => x.Id);
        var dtos = new List<FeeRefundRequestDto>();
        foreach (var item in items)
        {
            var dto = MapToDto(item, students);
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
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
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
            var dueIds = request.Lines.Select(x => x.DueFeeId).Distinct().ToList();
            var dues = await dueFeeRepository.GetListAsync(x => dueIds.Contains(x.Id));
            var allocationIds = request.Lines.Where(x => x.SourceAllocationId.HasValue)
                .Select(x => x.SourceAllocationId!.Value).Distinct().ToList();
            var allocations = await allocationRepository.GetListAsync(x => allocationIds.Contains(x.Id));
            dto.RefundSubledgerNetAmount = net;
            dto.IsRefundSubledgerBalanced = net == request.ProcessedAmount - request.ReversedAmount &&
                net == lineNet && postings.Count(x => !x.IsCorrection) <= 1 &&
                postings.Count(x => x.IsCorrection) <= 1 &&
                (request.Status != FeeRefundStatus.Processed || postings.Count(x => !x.IsCorrection) == 1) &&
                dues.Count == dueIds.Count && allocations.Count == allocationIds.Count &&
                request.Lines.All(line => line.SourceAllocationId.HasValue &&
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
    public async Task<RefundEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId)
    {
        await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);
        var assignments = await assignmentRepository.GetListAsync(x => x.StudentId == studentId && x.AcademicYearId == academicYearId);
        var assignment = assignments.OrderByDescending(x => x.CreationTime).FirstOrDefault()
            ?? throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned);
        if (!assignment.RefundPolicyId.HasValue || !assignment.RefundPolicyVersionId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.PolicyNotAssigned);
        var version = await policyVersionRepository.FindAsync(assignment.RefundPolicyVersionId.Value)
            ?? throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        RefundPolicySnapshot policy;
        try { policy = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson)!; }
        catch (JsonException) { throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid); }

        var dues = await dueFeeRepository.GetListAsync(x => x.StudentId == studentId &&
            x.StudentFeeStructureId == assignment.Id && x.PaidAmount > 0);
        if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads)
        {
            var hasPaidDues = dues.Count > 0;
            dues = dues.Where(x => policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)).ToList();
            if (hasPaidDues && dues.Count == 0)
                throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
        }
        var dueIds = dues.Select(x => x.Id).ToList();
        var collections = await collectionRepository.GetListAsync(x => x.StudentId == studentId &&
            x.AcademicYearId == academicYearId && x.Status != FeeCollectionStatus.Reversed &&
            x.Status != FeeCollectionStatus.InReconciliation);
        var collectionIds = collections.Select(x => x.Id).ToList();
        var receipts = await feePaymentReceiptRepository.GetListAsync(x => collectionIds.Contains(x.FeeCollectionTransactionId) &&
            x.ReceiptScope == FeeReceiptScope.Fee && (x.ReceiptKind == FeeReceiptKind.FinalFee ||
            (x.ReceiptKind == FeeReceiptKind.ProvisionalFee && x.VerificationStatus == FeeReceiptVerificationStatus.Verified)));
        var receiptByCollection = receipts.GroupBy(x => x.FeeCollectionTransactionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ReceiptKind == FeeReceiptKind.FinalFee).ThenByDescending(x => x.CreationTime).First());
        var eligibleCollectionIds = receiptByCollection.Keys.ToList();
        var allocations = await allocationRepository.GetListAsync(x => eligibleCollectionIds.Contains(x.FeeRecordMetadataId) && dueIds.Contains(x.DueFeeId));
        var allocationIds = allocations.Select(x => x.Id).ToList();
        var committed = await feeRefundRepository.GetCommittedAllocationAmountsAsync(allocationIds);
        var dueRemaining = dues.ToDictionary(x => x.Id, x => x.RefundableBalance);
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
        var grouped = rows.GroupBy(x => dueMap[x.DueFeeId].FeeHeadId).Select(g => new RefundEligibilityFeeHeadDto
        {
            FeeHeadId = g.Key, FeeHeadName = heads.GetValueOrDefault(g.Key, "Fee head"),
            TotalPaid = g.Sum(x => x.Paid), TotalRefunded = g.Sum(x => x.Refunded),
            TotalRefundable = g.Sum(x => x.Refundable), Months = g.ToList()
        }).ToList();
        var totalPaid = allocations.Sum(x => Convert.ToDecimal(x.Amount));
        var totalCommitted = allocationIds.Sum(x => committed.GetValueOrDefault(x));
        var studentYearCommitted = await feeRefundRepository.GetStudentYearCommittedAmountAsync(studentId, academicYearId);
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
            StudentId = studentId, AcademicYearId = academicYearId, StudentFeeStructureId = assignment.Id,
            RefundPolicyId = version.PolicyId, RefundPolicyVersionId = version.Id, RefundPolicyName = policy.PolicyName,
            AllowedPayoutMethods = policy.AllowedPayoutMethods.Select(ToPayoutMethodLabel).ToList(),
            ProcessingTargetDays = policy.ProcessingTargetDays, AllowPartialRefund = policy.AllowPartialRefund, TotalPaid = totalPaid,
            TotalRefunded = totalCommitted, TotalRefundable = totalRefundable, FeeHeads = grouped,
            Receipts = rows.GroupBy(x => x.SourceReceiptId).Select(g =>
            {
                var receipt = receipts.Single(x => x.Id == g.Key);
                return new RefundEligibleReceiptDto { ReceiptId = receipt.Id, ReceiptNumber = receipt.ReceiptNumber,
                    ReceiptDate = receipt.ReceiptDate, PaidAmount = g.Sum(x => x.Paid), RefundableAmount = g.Sum(x => x.Refundable) };
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
