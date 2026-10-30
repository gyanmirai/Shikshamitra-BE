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
			input.OriginalReceiptId == Guid.Empty ||
			input.Lines.Any(x => x.SourceAllocationId == Guid.Empty ||
				(x.DueFeeId.HasValue ? 1 : 0) + (x.ChargeId.HasValue ? 1 : 0) + (x.LateFeeDueFeeId.HasValue ? 1 : 0) != 1 ||
				x.DueFeeId == Guid.Empty || x.ChargeId == Guid.Empty || x.LateFeeDueFeeId == Guid.Empty) ||
			input.Lines.Select(x => x.SourceAllocationId).Distinct().Count() != input.Lines.Count ||
			input.PaymentDetails.Keys.Any(x => !allowed.Contains(x, StringComparer.Ordinal)) ||
			allowed.Any(x => !input.PaymentDetails.TryGetValue(x, out var value) || string.IsNullOrWhiteSpace(value) || value.Length > 200) ||
			string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000)
			throw new BusinessException(FamilyFinanceErrorCodes.InvalidRefund);
		return new CreateFeeRefundRequestDto { StudentId = child, AcademicYearId = year,
			OriginalReceiptId = input.OriginalReceiptId, IdempotencyKey = input.IdempotencyKey ?? string.Empty,
			RefundMethod = input.RefundMethod, Reason = input.Reason,
			PaymentDetails = input.PaymentDetails.ToDictionary(x => x.Key, x => (object?)x.Value),
			Lines = input.Lines.Select(x => new CreateFeeRefundLineDto { DueFeeId = x.DueFeeId,
				ChargeId = x.ChargeId, LateFeeDueFeeId = x.LateFeeDueFeeId,
				SourceAllocationId = x.SourceAllocationId, Amount = MobileFinanceCommandRules.ParseMoney(x.Amount) }).ToList() };
	}

	public virtual async Task<MobileFeeRefundPreview> FamilyRefundPreviewAsync(Guid child, Guid year, MobileRefundInput input)
	{
		await RequireFamilyRefundAsync(child, year);
		var prepared = await PrepareFamilyRefundAsync(child, year, input);
		return new MobileFeeRefundPreview { PreviewFingerprint=prepared.Hash,StudentFeeStructureId=prepared.AssignmentId,
   RefundPolicyId=prepared.PolicyId,RefundPolicyVersionId=prepared.PolicyVersionId,RefundPolicyName=prepared.Policy.PolicyName,
   SettledPaidAmount=MobileFinanceProjection.Money(prepared.Calculation.SettledPaidAmount),PriorProcessedRefund=MobileFinanceProjection.Money(prepared.Calculation.PriorProcessedRefund),
   ActiveReservation=MobileFinanceProjection.Money(prepared.Calculation.ActiveReservation),RequestedAmount=MobileFinanceProjection.Money(prepared.Lines.Sum(x=>x.Amount)),
   PolicyCalculatedAmount=MobileFinanceProjection.Money(Math.Min(prepared.Calculation.RawRefund,prepared.Calculation.RemainingEligible)),
   DeductionAmount=MobileFinanceProjection.Money(prepared.Calculation.DeductionAmount),CapAdjustment=MobileFinanceProjection.Money(prepared.Calculation.CapAdjustment),
   MaximumAllowedAmount=MobileFinanceProjection.Money(prepared.Calculation.FinalRefund),IsAllowed=true };
	}

	[UnitOfWork(IsDisabled = true)]
	public virtual async Task<MobileRefundRequest> FamilyRefundCreateAsync(Guid child, Guid year, MobileRefundInput input)
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
			return MobileFinanceReadService.RefundDto(saved,saved.Reason);
		}
		var create = FamilyRefundInput(child, year, input);
		var prepared = await PrepareFamilyRefundAsync(child, year, input);
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
		return MobileFinanceReadService.RefundDto(request,request.Reason);
	}

	private async Task<PreparedRefund> PrepareFamilyRefundAsync(Guid child, Guid year, MobileRefundInput input)
	{
		var prepared = await PrepareAsync(FamilyRefundInput(child, year, input), familyAuthorized: true);
		if (!prepared.Policy.AllowPartialRefund && prepared.Policy.AmountMode == RefundAmountMode.ManualUpToEligible &&
			prepared.Lines.Any(x => x.Amount != x.SettledPaidAmount))
			throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount);
		return prepared;
	}
}
