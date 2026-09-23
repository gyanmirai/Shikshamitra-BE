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
        var (items, count) = await feeRefundRepository.GetListWithDetailsAsync(input.StudentId,
            input.AcademicMediumId, input.StandardId, input.DivisionId, input.Status, input.Search,
            input.SkipCount, input.MaxResultCount, input.Sorting, scoped);
        var students = await BuildStudentMapAsync(items.Select(x => x.StudentId).Distinct());
        return new PagedResultDto<FeeRefundRequestDto>(count, items.Select(x => MapToDto(x, students)).ToList());
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<FeeRefundRequestDto> GetAsync(Guid id)
    {
        var request = await RequireAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        var dto = await MapAsync(request);
        if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.ViewSensitivePaymentDetails))
            dto.PaymentDetails = MapRefundPaymentDetailsToDict(request.RefundPaymentDetails);
        await PopulateRefundDetailsAsync(dto, request);
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
            dues = dues.Where(x => policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)).ToList();
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
        var calculation = refundPolicyCalculationService.Calculate(policy, new RefundPolicyCalculationInput(
            totalPaid, totalCommitted, 0m, studentYearCommitted,
            policy.AmountMode == RefundAmountMode.ManualUpToEligible ? grouped.Sum(x => x.TotalRefundable) : null));
        return new RefundEligibilityDto
        {
            StudentId = studentId, AcademicYearId = academicYearId, StudentFeeStructureId = assignment.Id,
            RefundPolicyId = version.PolicyId, RefundPolicyVersionId = version.Id, RefundPolicyName = policy.PolicyName,
            AllowedPayoutMethods = policy.AllowedPayoutMethods.Select(ToPayoutMethodLabel).ToList(),
            ProcessingTargetDays = policy.ProcessingTargetDays, AllowPartialRefund = policy.AllowPartialRefund, TotalPaid = totalPaid,
            TotalRefunded = totalCommitted, TotalRefundable = calculation.FinalRefund, FeeHeads = grouped,
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
