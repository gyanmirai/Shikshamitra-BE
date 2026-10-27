using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

[Volo.Abp.DependencyInjection.ExposeServices(typeof(FeeRefundAppService), typeof(IFeeRefundAppService), typeof(IFamilyRefundService))]
public partial class FeeRefundAppService : IFamilyRefundService
{
	private Task<MobileFinanceAccessContext> RequireFamilyRefundAsync(Guid child, Guid year) =>
		LazyServiceProvider.LazyGetRequiredService<MobileFinanceAccessService>().RequireAsync(child, year,
			guardianOnly: true, permission: MobileFinancePermissions.RequestRefund);

	private static CreateFeeRefundRequestDto FamilyRefundInput(Guid child, Guid year, MobileRefundInput input)
	{
		// Parent beneficiaries never supply school bank routing or payout evidence.
		var allowed = input.RefundMethod switch
		{
			"Cash" => new[] { "receivedBy" },
			"UPI" => new[] { "beneficiaryName", "upiId" },
			"Cheque" or "Demand Draft (DD)" => new[] { "payeeName" },
			"Bank Transfer (NEFT/RTGS/IMPS)" => new[] { "beneficiaryName", "beneficiaryBankName", "beneficiaryAccountNumber", "ifscCode" },
			_ => Array.Empty<string>()
		};
		if (allowed.Length == 0 || input.Lines.Count is 0 or > 100 || input.PaymentDetails == null ||
			input.PaymentDetails.Keys.Any(x => !allowed.Contains(x, StringComparer.Ordinal)) ||
			allowed.Any(x => !input.PaymentDetails.TryGetValue(x, out var value) || string.IsNullOrWhiteSpace(value) || value.Length > 200) ||
			string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000)
			throw new BusinessException(FamilyFinanceErrorCodes.InvalidRefund);
		return new CreateFeeRefundRequestDto { StudentId = child, AcademicYearId = year,
			OriginalReceiptId = input.OriginalReceiptId, IdempotencyKey = input.IdempotencyKey ?? string.Empty,
			RefundMethod = input.RefundMethod, Reason = input.Reason,
			PaymentDetails = input.PaymentDetails.ToDictionary(x => x.Key, x => (object?)x.Value),
			Lines = input.Lines.Select(x => new CreateFeeRefundLineDto { DueFeeId = x.DueFeeId,
				SourceAllocationId = x.SourceAllocationId, Amount = MobileFinanceCommandRules.ParseMoney(x.Amount) }).ToList() };
	}

	public virtual async Task<object> FamilyRefundPreviewAsync(Guid child, Guid year, MobileRefundInput input)
	{
		await RequireFamilyRefundAsync(child, year);
		var prepared = await PrepareAsync(FamilyRefundInput(child, year, input), familyAuthorized: true);
		return new { Currency = "INR", PreviewFingerprint = prepared.Hash, Amount = MobileFinanceCommandRules.Money(prepared.Calculation.FinalRefund), prepared.Policy.PolicyName };
	}

	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<MobileFinanceCommandResult> FamilyRefundCreateAsync(Guid child, Guid year, MobileRefundInput input)
	{
		await RequireFamilyRefundAsync(child, year);
		await LazyServiceProvider.LazyGetRequiredService<MobileFinanceAccessService>().RequireAsync(child, year,
			guardianOnly: true, permission: MobileFinancePermissions.SubmitRefund);
		using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var store = LazyServiceProvider.LazyGetRequiredService<MobileFinanceCommandStore>();
		var payload = new { child, year, input };
		var replay = await store.ReplayAsync("refund_create", input.IdempotencyKey, payload);
		if (replay.HasValue)
		{
			var saved = await RequireAsync(replay.Value);
			MobileFinanceCommandRules.RequireOwner(child, year, currentUser.GetId(), saved.StudentId ?? Guid.Empty, saved.AcademicYearId, saved.CreatorId);
			await uow.CompleteAsync();
			return new(saved.Id, "refund", saved.Status.ToString(), MobileFinanceCommandRules.Money(saved.CalculatedAmount), []);
		}
		var create = FamilyRefundInput(child, year, input);
		var prepared = await PrepareAsync(create, familyAuthorized: true);
		MobileFinanceCommandRules.RequireFingerprint(prepared.Hash, input.PreviewFingerprint);
		// Existing refund request keys are tenant-wide; namespace family keys by actor to prevent cross-user replay.
		var domainKey = "family:" + MobileFinanceCommandRules.Hash(new { actor = currentUser.GetId(), input.IdempotencyKey });
		var request = await feeRefundManager.CreatePendingAsync(prepared.StudentId, year, prepared.AssignmentId,
			prepared.PolicyId, prepared.PolicyVersionId, prepared.Receipt.Id, prepared.Receipt.ReceiptNumber, prepared.Lines,
			create.RefundMethod, FeeRefundManager.MapRefundPaymentDetails(create.RefundMethod, create.PaymentDetails), create.Reason,
			prepared.PolicyJson, prepared.CalculationJson, prepared.Hash, currentUser.GetId(), domainKey, replacesRequestId: null, financeFeeAccountId: prepared.FinanceFeeAccountId);
		await WriteAuditAsync(request, FinanceAuditActions.Create, "Family refund submitted for staff review.");
		await store.RecordAsync("refund_create", input.IdempotencyKey, payload, request.Id, child, year);
		await uow.CompleteAsync();
		return new(request.Id, "refund", request.Status.ToString(), MobileFinanceCommandRules.Money(request.CalculatedAmount), []);
	}

    public virtual async Task<object> FamilyRefundEligibilityAsync(Guid child, Guid year)
    {
        await RequireFamilyRefundAsync(child, year);
        var result = await FamilyEligibilityCoreAsync(child, year);
        return MobileFinanceRefundEligibilityProjection.Map(result);
    }
    private async Task<RefundEligibilityDto> FamilyEligibilityCoreAsync(Guid child, Guid academicYearId)
    {
        Guid? studentId = child; Guid? financeFeeAccountId = null; Guid? originalReceiptId = null;
        if (originalReceiptId.HasValue)
        {
            var sourceReceipt = await feePaymentReceiptRepository.FindAsync(originalReceiptId.Value)
                ?? throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            if (sourceReceipt.StudentId != child) throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            if (sourceReceipt.AcademicYearId != academicYearId ||
                financeFeeAccountId.HasValue && sourceReceipt.FinanceFeeAccountId != financeFeeAccountId ||
                !sourceReceipt.FinanceFeeAccountId.HasValue && studentId.HasValue && sourceReceipt.StudentId != studentId)
                throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid);
            financeFeeAccountId = sourceReceipt.FinanceFeeAccountId;
            studentId ??= sourceReceipt.StudentId;
        }
        var payer = await RequirePolicyEvidenceAsync(studentId, academicYearId, financeFeeAccountId, familyAuthorized: true);
        var version = payer.Version;
        var policy = payer.Policy;
        var dues = await dueFeeRepository.GetListAsync(x => x.PaidAmount > 0 && (payer.FinanceFeeAccountId.HasValue
            ? x.FinanceFeeAccountId == payer.FinanceFeeAccountId
            : x.StudentId == payer.StudentId && x.StudentFeeStructureId == payer.AssignmentId));
        if (policy.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads)
        {
            var hasPaidDues = dues.Count > 0;
            dues = dues.Where(x => policy.ApplicableFeeHeadIds.Contains(x.FeeHeadId)).ToList();
            if (hasPaidDues && dues.Count == 0)
                throw new BusinessException(FeeRefundErrorCodes.FeeHeadNotEligible);
        }
        var dueIds = dues.Select(x => x.Id).ToList();
        var collections = await collectionRepository.GetListAsync(x => (payer.FinanceFeeAccountId.HasValue
            ? x.FinanceFeeAccountId == payer.FinanceFeeAccountId : x.StudentId == payer.StudentId) &&
            x.AcademicYearId == academicYearId && x.Status != FeeCollectionStatus.Reversed &&
            x.Status != FeeCollectionStatus.InReconciliation);
        var collectionIds = collections.Select(x => x.Id).ToList();
        var receipts = await feePaymentReceiptRepository.GetListAsync(x => x.StudentId == child && x.AcademicYearId == academicYearId && collectionIds.Contains(x.FeeCollectionTransactionId) &&
            (!originalReceiptId.HasValue || x.Id == originalReceiptId.Value) &&
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
        var grouped = rows.GroupBy(x => dueMap[x.DueFeeId!.Value].FeeHeadId).Select(g => new RefundEligibilityFeeHeadDto
        {
            FeeHeadId = g.Key, FeeHeadName = heads.GetValueOrDefault(g.Key, "Fee head"),
            TotalPaid = g.Sum(x => x.Paid), TotalRefunded = g.Sum(x => x.Refunded),
            TotalRefundable = g.Sum(x => x.Refundable), Months = g.ToList()
        }).ToList();
        var totalPaid = allocations.Sum(x => Convert.ToDecimal(x.Amount));
        var totalCommitted = allocationIds.Sum(x => committed.GetValueOrDefault(x));
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
                var receipt = receipts.Single(x => x.Id == g.Key);
                return new RefundEligibleReceiptDto { ReceiptId = receipt.Id, ReceiptNumber = receipt.ReceiptNumber,
                    ReceiptDate = receipt.ReceiptDate, PaidAmount = g.Sum(x => x.Paid), RefundableAmount = g.Sum(x => x.Refundable) };
            }).OrderByDescending(x => x.ReceiptDate).ToList()
        };
    }


}
