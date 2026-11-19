using System;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Domain.Finance.Policies;

internal static class RefundPolicyMapping
{
	public static RefundPolicyStatus ParseStatus(string? value) => Normalize(value) switch
	{
		"draft" => RefundPolicyStatus.Draft,
		"active" => RefundPolicyStatus.Active,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidStatus)
	};

	public static RefundFeeHeadScope ParseFeeHeadScope(string? value) => Normalize(value) switch
	{
		"allfeeheads" => RefundFeeHeadScope.AllFeeHeads,
		"selectedfeeheads" => RefundFeeHeadScope.SelectedFeeHeads,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidFeeHeadReference)
	};

	public static RefundAmountMode ParseAmountMode(string? value) => Normalize(value) switch
	{
		"remainingpaidamount" => RefundAmountMode.RemainingPaidAmount,
		"manualuptoeligible" => RefundAmountMode.ManualUpToEligible,
		"fixedamount" => RefundAmountMode.FixedAmount,
		"percentage" => RefundAmountMode.Percentage,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidAmountRule)
	};

	public static RefundDeductionType ParseDeductionType(string? value) => Normalize(value) switch
	{
		"none" => RefundDeductionType.None,
		"fixedamount" => RefundDeductionType.FixedAmount,
		"percentage" => RefundDeductionType.Percentage,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidDeductionRule)
	};

	public static RefundPayoutMethod ParsePayoutMethod(string? value) => Normalize(value) switch
	{
		"cash" => RefundPayoutMethod.Cash,
		"cheque" => RefundPayoutMethod.Cheque,
		"demanddraft" => RefundPayoutMethod.DemandDraft,
		"banktransfer" => RefundPayoutMethod.BankTransfer,
		"upi" => RefundPayoutMethod.Upi,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidPayoutMethod)
	};

	public static RoundingMethodType ParseRounding(string? value) => Normalize(value) switch
	{
		"roundup" => RoundingMethodType.RoundUp,
		"rounddown" => RoundingMethodType.RoundDown,
		"nearestrupee" => RoundingMethodType.NearestRupee,
		"none" => RoundingMethodType.None,
		_ => throw new BusinessException(RefundPolicyErrorCodes.InvalidAmountRule)
	};

	public static RefundPolicyUpdateModel ToUpdateModel(CreateUpdateRefundPolicyDto input, string policyCode) => new(
		input.PolicyName,
		policyCode,
		input.Description,
		ParseStatus(input.Status),
		input.IsDefault,
		ParseFeeHeadScope(input.FeeHeadScope),
		input.ApplicableFeeHeadIds,
		input.IncludePaidCharges,
		input.IncludePaidLateFee,
		input.AllowPartialRefund,
		ParseAmountMode(input.AmountMode),
		input.PercentageValue,
		input.FixedAmount,
		input.MaximumPerRequest,
		input.MaximumPerStudentYear,
		ParseRounding(input.RoundingMethod),
		ParseDeductionType(input.DeductionType),
		input.DeductionValue,
		input.MaximumDeduction,
		input.DeductionExplanation,
		input.AllowedPayoutMethods.Select(ParsePayoutMethod).ToList(),
		input.ProcessingTargetDays, input.ExpenseCategoryId, input.DefaultPayingBankAccountId);

	public static RefundPolicyDto ToDto(RefundPolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		PolicyName = entity.PolicyName,
		PolicyCode = entity.PolicyCode,
		Description = entity.Description,
		Status = entity.Status.ToString(),
		IsDefault = entity.IsDefault,
		FeeHeadScope = entity.FeeHeadScope.ToString(),
		ApplicableFeeHeadIds = [.. entity.GetApplicableFeeHeadIds()],
		IncludePaidCharges = entity.IncludePaidCharges,
		IncludePaidLateFee = entity.IncludePaidLateFee,
		AllowPartialRefund = entity.AllowPartialRefund,
		AmountMode = entity.AmountMode.ToString(),
		PercentageValue = entity.PercentageValue,
		FixedAmount = entity.FixedAmount,
		MaximumPerRequest = entity.MaximumPerRequest,
		MaximumPerStudentYear = entity.MaximumPerStudentYear,
		RoundingMethod = entity.RoundingMethod.ToString(),
		DeductionType = entity.DeductionType.ToString(),
		DeductionValue = entity.DeductionValue,
		MaximumDeduction = entity.MaximumDeduction,
		DeductionExplanation = entity.DeductionExplanation,
		AllowedPayoutMethods = entity.GetAllowedPayoutMethods().Select(x => x.ToString()).ToList(),
		ProcessingTargetDays = entity.ProcessingTargetDays,
		ExpenseCategoryId = entity.ExpenseCategoryId,
		DefaultPayingBankAccountId = entity.DefaultPayingBankAccountId,
		Version = entity.Version,
		CreationTime = entity.CreationTime,
		CreatorId = entity.CreatorId,
		LastModificationTime = entity.LastModificationTime,
		LastModifierId = entity.LastModifierId,
		ConcurrencyStamp = entity.ConcurrencyStamp
	};

	public static string ToSnapshotJson(RefundPolicy entity) => JsonSerializer.Serialize(entity.ToSnapshot());

	private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
