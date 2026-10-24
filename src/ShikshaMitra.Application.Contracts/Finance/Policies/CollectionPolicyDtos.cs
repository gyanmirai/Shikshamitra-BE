using System;
using System.Collections.Generic;
using FluentValidation;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionPolicyEscalationRuleDto
{
	public int StartDay { get; set; }
	public int? EndDay { get; set; }
	public string CalculationType { get; set; } = "Flat";
	public decimal Value { get; set; }
	public decimal? MaxFee { get; set; }
}

/// <summary>The subset of policy-level late-fee fields a fee head can override — only meaningful when OverrideMode = "CustomRule".</summary>
public class LateFeeRuleOverrideDto
{
	public string LateFeeType { get; set; } = "Flat";
	public decimal? FlatAmount { get; set; }
	public decimal? Percentage { get; set; }
	public string PercentageBase { get; set; } = "OutstandingAmount";
	public decimal? AmountPerDay { get; set; }
	public decimal? AmountPerWeek { get; set; }
	public decimal? AmountPerMonth { get; set; }
	public string CompoundFrequency { get; set; } = "Daily";
	public List<CollectionPolicyEscalationRuleDto> EscalationRules { get; set; } = new();
	public string RoundingMethod { get; set; } = "RoundUp";
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }
}

/// <summary>Per-fee-head deviation from the policy-level late-fee rule (Phase 4a).</summary>
public class CollectionPolicyFeeHeadOverrideDto
{
	public Guid FeeHeadId { get; set; }
	/// <summary>"Inherit" | "NoLateFee" | "CustomRule"</summary>
	public string OverrideMode { get; set; } = "Inherit";
	public LateFeeRuleOverrideDto? LateFeeRule { get; set; }
}

public class CollectionPolicyDto : EntityDto<Guid>, ICurrentPolicyDto
{
	public Guid? CurrentVersionId { get; set; }
	public int CurrentVersionNumber => Version;
	public bool IsAttachable => Status == "Active" && CurrentVersionId.HasValue;
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Status { get; set; } = "Draft";
	public bool IsDefault { get; set; }
	public string PolicyType { get; set; } = "collection";

	public bool EnableLateFee { get; set; }
	public int GracePeriodDays { get; set; }
	public List<Guid> LateFeeApplyOnFeeHeads { get; set; } = new();
	public string LateFeeType { get; set; } = "Flat";
	public decimal? FlatAmount { get; set; }
	public decimal? Percentage { get; set; }
	public string PercentageBase { get; set; } = "OutstandingAmount";
	public decimal? AmountPerDay { get; set; }
	public decimal? AmountPerWeek { get; set; }
	public decimal? AmountPerMonth { get; set; }
	public string CompoundFrequency { get; set; } = "Daily";
	public List<CollectionPolicyEscalationRuleDto> EscalationRules { get; set; } = new();
	public List<CollectionPolicyFeeHeadOverrideDto> FeeHeadOverrides { get; set; } = new();
	public string RoundingMethod { get; set; } = "RoundUp";
	public bool ConsiderAsIncome { get; set; }
	public Guid? LateFeeIncomeHeadId { get; set; }
	public Guid? LateFeeIncomeCategoryId { get; set; }
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }

	public bool AllowOnlineCollection { get; set; }
	public string AllocationBasis { get; set; } = "Legacy";
	public string ReceiptGrouping { get; set; } = "Separate";

	public int Version { get; set; }
	public bool IsUsedInDownstream { get; set; }
	public DateTime CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
	public DateTime? LastModificationTime { get; set; }
	public Guid? LastModifierId { get; set; }
}

public class CreateUpdateCollectionPolicyDto
{
	public Guid AcademicYearId { get; set; }
	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Status { get; set; } = "Draft";
	public bool IsDefault { get; set; }
	public string? ChangeReason { get; set; }

	public bool EnableLateFee { get; set; }
	public int GracePeriodDays { get; set; }
	public List<Guid> LateFeeApplyOnFeeHeads { get; set; } = new();
	public string LateFeeType { get; set; } = "Flat";
	public decimal? FlatAmount { get; set; }
	public decimal? Percentage { get; set; }
	public string PercentageBase { get; set; } = "OutstandingAmount";
	public decimal? AmountPerDay { get; set; }
	public decimal? AmountPerWeek { get; set; }
	public decimal? AmountPerMonth { get; set; }
	public string CompoundFrequency { get; set; } = "Daily";
	public List<CollectionPolicyEscalationRuleDto> EscalationRules { get; set; } = new();
	public List<CollectionPolicyFeeHeadOverrideDto> FeeHeadOverrides { get; set; } = new();
	public string RoundingMethod { get; set; } = "RoundUp";
	public bool ConsiderAsIncome { get; set; }
	public Guid? LateFeeIncomeHeadId { get; set; }
	public Guid? LateFeeIncomeCategoryId { get; set; }
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }

	public bool AllowOnlineCollection { get; set; } = true;
	/// <summary>Omitted on older clients: preserve the saved mode on update; Legacy on create.</summary>
	public string? AllocationBasis { get; set; }
	public string? ReceiptGrouping { get; set; }
}

public class CreateUpdateCollectionPolicyDtoValidator : AbstractValidator<CreateUpdateCollectionPolicyDto>
{
	public CreateUpdateCollectionPolicyDtoValidator()
	{
		RuleFor(x => x.ReceiptGrouping).Must(value => value is null or "Separate" or "Combined")
			.WithMessage("Choose single combined or separate receipts.");
		RuleFor(x => x.AllocationBasis).Must(value => value is null or "Legacy" or "Installment" or "FeeHead")
			.WithMessage("Choose By installment or By fee head.");
		RuleFor(x => x.ChangeReason).MaximumLength(500);
		RuleFor(x => x.GracePeriodDays).InclusiveBetween(0, CollectionPolicyNumericLimits.MaximumDays);
		RuleFor(x => x.MaximumChargingDays).InclusiveBetween(1, CollectionPolicyNumericLimits.MaximumDays);
		RuleFor(x => x.FlatAmount).Must(IsOptionalAmount).WithMessage("Enter an amount between 0.01 and 999999 with up to 2 decimals.");
		RuleFor(x => x.AmountPerDay).Must(IsOptionalAmount).WithMessage("Enter an amount between 0.01 and 999999 with up to 2 decimals.");
		RuleFor(x => x.AmountPerWeek).Must(IsOptionalAmount).WithMessage("Enter an amount between 0.01 and 999999 with up to 2 decimals.");
		RuleFor(x => x.AmountPerMonth).Must(IsOptionalAmount).WithMessage("Enter an amount between 0.01 and 999999 with up to 2 decimals.");
		RuleFor(x => x.MaximumLateFeeAmount).Must(IsOptionalAmount).WithMessage("Enter an amount between 0.01 and 999999 with up to 2 decimals, or leave empty for no limit.");
		RuleFor(x => x.Percentage).Must(value => !value.HasValue || CollectionPolicyNumericLimits.IsPercentage(value.Value))
			.WithMessage("Enter a percentage between 0.01 and 100 with up to 2 decimals.");
		RuleForEach(x => x.EscalationRules).SetValidator(new CollectionPolicyEscalationRuleDtoValidator());
		RuleFor(x => x).Must(x => !x.IsDefault ||
			string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Only an Active collection policy can be the default.");
	}

	private static bool IsOptionalAmount(decimal? value) => !value.HasValue || CollectionPolicyNumericLimits.IsAmount(value.Value);
}

public class CollectionPolicyEscalationRuleDtoValidator : AbstractValidator<CollectionPolicyEscalationRuleDto>
{
	public CollectionPolicyEscalationRuleDtoValidator()
	{
		RuleFor(x => x.StartDay).InclusiveBetween(1, CollectionPolicyNumericLimits.MaximumDays);
		RuleFor(x => x.EndDay).InclusiveBetween(1, CollectionPolicyNumericLimits.MaximumDays);
		RuleFor(x => x.EndDay).Must((rule, end) => !end.HasValue || end >= rule.StartDay)
			.WithMessage("To Day must be greater than or equal to From Day.");
		RuleFor(x => x.Value).Must((rule, value) => string.Equals(rule.CalculationType, "Percentage", StringComparison.OrdinalIgnoreCase)
			? CollectionPolicyNumericLimits.IsPercentage(value) : CollectionPolicyNumericLimits.IsAmount(value))
			.WithMessage("Enter a valid slab amount or percentage with up to 2 decimals.");
		RuleFor(x => x.MaxFee).Must(value => !value.HasValue || CollectionPolicyNumericLimits.IsAmount(value.Value));
	}
}

public class CollectionPolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Status { get; set; }
	public bool IncludeArchived { get; set; }
	public string? Search { get; set; }
	public string? PolicyNameFilter { get; set; }
	public string? PolicyNameOperator { get; set; }
	public bool? EnableLateFee { get; set; }
	public string? LateFeeType { get; set; }
	public string? GraceDaysOperator { get; set; }
	public int? GraceDaysValue { get; set; }
	public int? GraceDaysMax { get; set; }
	public bool? AllowOnlineCollection { get; set; }
	public string? UpdatedAtOperator { get; set; }
	public DateTime? UpdatedAt { get; set; }
	public DateTime? UpdatedAtEnd { get; set; }
}

public class CollectionPolicyCloneRequestDto
{
	public string? Name { get; set; }
	public Guid? TargetAcademicYearId { get; set; }
}

public class CollectionPolicySimulateRequestDto
{
	public int DaysLate { get; set; }
	public decimal OutstandingAmount { get; set; }
	public decimal? OriginalFeeAmount { get; set; }
	public decimal? RemainingBalance { get; set; }
	public Guid? FeeHeadId { get; set; }
	/// <summary>When provided, resolves the student's StudentFeePolicyOverride (exempt/custom grace) instead of the input flags below.</summary>
	public Guid? StudentId { get; set; }
	public Guid? FeeStructureId { get; set; }
	public bool IsLateFeeExempt { get; set; }
	public int? CustomGracePeriodDays { get; set; }
}

public class CollectionPolicySimulateRequestDtoValidator : AbstractValidator<CollectionPolicySimulateRequestDto>
{
	public CollectionPolicySimulateRequestDtoValidator()
	{
		RuleFor(x => x.DaysLate).InclusiveBetween(0, CollectionPolicyNumericLimits.MaximumDays);
		RuleFor(x => x.CustomGracePeriodDays).InclusiveBetween(0, CollectionPolicyNumericLimits.MaximumDays);
		RuleFor(x => x.OutstandingAmount).Must(IsSimulationAmount);
		RuleFor(x => x.OriginalFeeAmount).Must(value => !value.HasValue || IsSimulationAmount(value.Value));
		RuleFor(x => x.RemainingBalance).Must(value => !value.HasValue || IsSimulationAmount(value.Value));
	}

	private static bool IsSimulationAmount(decimal value) => value >= 0 &&
		value <= CollectionPolicyNumericLimits.MaximumAmount && CollectionPolicyNumericLimits.HasCurrencyPrecision(value);
}

public class CollectionPolicySimulateResponseDto
{
	public decimal LateFee { get; set; }
	public bool IsExempt { get; set; }
	public int EffectiveDaysLate { get; set; }
	public int EffectiveGracePeriodDays { get; set; }
	public string? ExceptionWindowApplied { get; set; }
	public string FeeHeadOverrideApplied { get; set; } = "Inherit";
	public decimal AmountBeforeCap { get; set; }
	public string? CappedBy { get; set; }
	public string RoundingApplied { get; set; } = "None";
	public string Explanation { get; set; } = string.Empty;

}
