using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Policies;

public class RefundPolicy : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }
	public Guid AcademicYearId { get; protected set; }
	public string PolicyName { get; protected set; } = string.Empty;
	public string PolicyCode { get; protected set; } = string.Empty;
	public string Description { get; protected set; } = string.Empty;
	public RefundPolicyStatus Status { get; protected set; }
	public bool IsDefault { get; protected set; }
	public RefundEligiblePaymentState EligiblePaymentState { get; protected set; }
	public RefundFeeHeadScope FeeHeadScope { get; protected set; }
	public string ApplicableFeeHeadIdsJson { get; protected set; } = "[]";
	public bool IncludePaidCharges { get; protected set; }
	public bool IncludePaidLateFee { get; protected set; }
	public int? RequestDeadlineDays { get; protected set; }
	public bool AllowPartialRefund { get; protected set; }
	public RefundAmountMode AmountMode { get; protected set; }
	public decimal? PercentageValue { get; protected set; }
	public decimal? FixedAmount { get; protected set; }
	public decimal? MaximumPerRequest { get; protected set; }
	public decimal? MaximumPerStudentYear { get; protected set; }
	public RoundingMethodType RoundingMethod { get; protected set; }
	public RefundDeductionType DeductionType { get; protected set; }
	public decimal? DeductionValue { get; protected set; }
	public decimal? MaximumDeduction { get; protected set; }
	public string DeductionExplanation { get; protected set; } = string.Empty;
	public string AllowedPayoutMethodsJson { get; protected set; } = "[]";
	public int? ProcessingTargetDays { get; protected set; }
	public int Version { get; protected set; }

	protected RefundPolicy()
	{
	}

	public RefundPolicy(Guid id, Guid? tenantId, Guid academicYearId, RefundPolicyUpdateModel model) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = 0;
		Update(model);
	}

	public void Update(RefundPolicyUpdateModel model)
	{
		if (Status == RefundPolicyStatus.Archived && Version > 0)
			throw new BusinessException(RefundPolicyErrorCodes.ArchivedPolicyNotEditable);
		if (AcademicYearId == Guid.Empty)
			throw new BusinessException(RefundPolicyErrorCodes.AcademicYearRequired);
		if (string.IsNullOrWhiteSpace(model.PolicyName))
			throw new BusinessException(RefundPolicyErrorCodes.NameRequired);
		if (string.IsNullOrWhiteSpace(model.PolicyCode))
			throw new BusinessException(RefundPolicyErrorCodes.CodeRequired);
		if (model.Status == RefundPolicyStatus.Archived)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidStatus);
		if (model.IsDefault && model.Status != RefundPolicyStatus.Active)
			throw new BusinessException(RefundPolicyErrorCodes.DefaultPolicyMustBeActive);

		ValidateRules(model);

		PolicyName = model.PolicyName.Trim();
		PolicyCode = model.PolicyCode.Trim().ToUpperInvariant();
		Description = model.Description?.Trim() ?? string.Empty;
		Status = model.Status;
		IsDefault = model.IsDefault;
		EligiblePaymentState = model.EligiblePaymentState;
		FeeHeadScope = model.FeeHeadScope;
		ApplicableFeeHeadIdsJson = JsonSerializer.Serialize(
			model.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads
				? model.ApplicableFeeHeadIds.Distinct().Order().ToList()
				: []);
		IncludePaidCharges = model.IncludePaidCharges;
		IncludePaidLateFee = model.IncludePaidLateFee;
		RequestDeadlineDays = model.RequestDeadlineDays;
		AllowPartialRefund = model.AllowPartialRefund;
		AmountMode = model.AmountMode;
		PercentageValue = model.AmountMode == RefundAmountMode.Percentage ? model.PercentageValue : null;
		FixedAmount = model.AmountMode == RefundAmountMode.FixedAmount ? model.FixedAmount : null;
		MaximumPerRequest = model.MaximumPerRequest;
		MaximumPerStudentYear = model.MaximumPerStudentYear;
		RoundingMethod = model.RoundingMethod;
		DeductionType = model.DeductionType;
		DeductionValue = model.DeductionType == RefundDeductionType.None ? null : model.DeductionValue;
		MaximumDeduction = model.DeductionType == RefundDeductionType.None ? null : model.MaximumDeduction;
		DeductionExplanation = model.DeductionType == RefundDeductionType.None
			? string.Empty
			: model.DeductionExplanation?.Trim() ?? string.Empty;
		AllowedPayoutMethodsJson = JsonSerializer.Serialize(model.AllowedPayoutMethods.Distinct().Order().ToList());
		ProcessingTargetDays = model.ProcessingTargetDays;
		Version++;
	}

	public void Archive()
	{
		Status = RefundPolicyStatus.Archived;
		IsDefault = false;
		Version++;
	}

	public void Unarchive()
	{
		Status = RefundPolicyStatus.Draft;
		IsDefault = false;
		Version++;
	}

	public void SetDefault(bool isDefault)
	{
		if (isDefault && Status != RefundPolicyStatus.Active)
			throw new BusinessException(RefundPolicyErrorCodes.DefaultPolicyMustBeActive);
		if (IsDefault == isDefault) return;
		IsDefault = isDefault;
		Version++;
	}

	public IReadOnlyList<Guid> GetApplicableFeeHeadIds() =>
		JsonSerializer.Deserialize<List<Guid>>(ApplicableFeeHeadIdsJson) ?? [];

	public IReadOnlyList<RefundPayoutMethod> GetAllowedPayoutMethods() =>
		JsonSerializer.Deserialize<List<RefundPayoutMethod>>(AllowedPayoutMethodsJson) ?? [];

	public RefundPolicySnapshot ToSnapshot() => new(
		Id,
		AcademicYearId,
		PolicyName,
		PolicyCode,
		Status,
		IsDefault,
		EligiblePaymentState,
		FeeHeadScope,
		GetApplicableFeeHeadIds(),
		IncludePaidCharges,
		IncludePaidLateFee,
		RequestDeadlineDays,
		AllowPartialRefund,
		AmountMode,
		PercentageValue,
		FixedAmount,
		MaximumPerRequest,
		MaximumPerStudentYear,
		RoundingMethod,
		DeductionType,
		DeductionValue,
		MaximumDeduction,
		DeductionExplanation,
		GetAllowedPayoutMethods(),
		ProcessingTargetDays,
		Version);

	private static void ValidateRules(RefundPolicyUpdateModel model)
	{
		if (model.FeeHeadScope == RefundFeeHeadScope.SelectedFeeHeads && model.ApplicableFeeHeadIds.Count == 0)
			throw new BusinessException(RefundPolicyErrorCodes.FeeHeadRequired);
		if (model.AllowedPayoutMethods.Count == 0)
			throw new BusinessException(RefundPolicyErrorCodes.PayoutMethodRequired);
		if (model.RequestDeadlineDays is < 0 || model.ProcessingTargetDays is < 0)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidDayRule);
		if (model.MaximumPerRequest is <= 0 || model.MaximumPerStudentYear is <= 0)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidLimit);
		if (model.MaximumPerRequest.HasValue && model.MaximumPerStudentYear.HasValue &&
			model.MaximumPerRequest > model.MaximumPerStudentYear)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidLimit);
		if (model.AmountMode == RefundAmountMode.FixedAmount && model.FixedAmount is null or <= 0)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidAmountRule);
		if (model.AmountMode == RefundAmountMode.Percentage && model.PercentageValue is null or <= 0 or > 100)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidAmountRule);
		if (model.AmountMode == RefundAmountMode.ManualUpToEligible && !model.AllowPartialRefund)
			throw new BusinessException(RefundPolicyErrorCodes.InvalidAmountRule);
		if (model.DeductionType != RefundDeductionType.None)
		{
			if (model.DeductionValue is null or <= 0 || string.IsNullOrWhiteSpace(model.DeductionExplanation))
				throw new BusinessException(RefundPolicyErrorCodes.InvalidDeductionRule);
			if (model.DeductionType == RefundDeductionType.Percentage && model.DeductionValue > 100)
				throw new BusinessException(RefundPolicyErrorCodes.InvalidDeductionRule);
			if (model.MaximumDeduction is <= 0)
				throw new BusinessException(RefundPolicyErrorCodes.InvalidDeductionRule);
		}
	}
}

public sealed record RefundPolicyUpdateModel(
	string PolicyName,
	string PolicyCode,
	string? Description,
	RefundPolicyStatus Status,
	bool IsDefault,
	RefundEligiblePaymentState EligiblePaymentState,
	RefundFeeHeadScope FeeHeadScope,
	IReadOnlyCollection<Guid> ApplicableFeeHeadIds,
	bool IncludePaidCharges,
	bool IncludePaidLateFee,
	int? RequestDeadlineDays,
	bool AllowPartialRefund,
	RefundAmountMode AmountMode,
	decimal? PercentageValue,
	decimal? FixedAmount,
	decimal? MaximumPerRequest,
	decimal? MaximumPerStudentYear,
	RoundingMethodType RoundingMethod,
	RefundDeductionType DeductionType,
	decimal? DeductionValue,
	decimal? MaximumDeduction,
	string? DeductionExplanation,
	IReadOnlyCollection<RefundPayoutMethod> AllowedPayoutMethods,
	int? ProcessingTargetDays);

public sealed record RefundPolicySnapshot(
	Guid PolicyId,
	Guid AcademicYearId,
	string PolicyName,
	string PolicyCode,
	RefundPolicyStatus Status,
	bool IsDefault,
	RefundEligiblePaymentState EligiblePaymentState,
	RefundFeeHeadScope FeeHeadScope,
	IReadOnlyList<Guid> ApplicableFeeHeadIds,
	bool IncludePaidCharges,
	bool IncludePaidLateFee,
	int? RequestDeadlineDays,
	bool AllowPartialRefund,
	RefundAmountMode AmountMode,
	decimal? PercentageValue,
	decimal? FixedAmount,
	decimal? MaximumPerRequest,
	decimal? MaximumPerStudentYear,
	RoundingMethodType RoundingMethod,
	RefundDeductionType DeductionType,
	decimal? DeductionValue,
	decimal? MaximumDeduction,
	string DeductionExplanation,
	IReadOnlyList<RefundPayoutMethod> AllowedPayoutMethods,
	int? ProcessingTargetDays,
	int Version);
