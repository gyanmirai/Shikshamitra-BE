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
	public string RoundingMethod { get; set; } = "None";
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
	public string GracePeriodType { get; set; } = "Calendar";
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
	public string RoundingMethod { get; set; } = "None";
	public bool ConsiderAsIncome { get; set; }
	public Guid? LateFeeIncomeHeadId { get; set; }
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }

	public bool AllowOnlineCollection { get; set; }

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
	public string GracePeriodType { get; set; } = "Calendar";
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
	public string RoundingMethod { get; set; } = "None";
	public bool ConsiderAsIncome { get; set; }
	public Guid? LateFeeIncomeHeadId { get; set; }
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }

	public bool AllowOnlineCollection { get; set; } = true;
}

public class CreateUpdateCollectionPolicyDtoValidator : AbstractValidator<CreateUpdateCollectionPolicyDto>
{
	public CreateUpdateCollectionPolicyDtoValidator()
	{
		RuleFor(x => x.ChangeReason).MaximumLength(500);
		RuleFor(x => x).Must(x => !x.IsDefault ||
			string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
			.WithMessage("Only an Active collection policy can be the default.");
	}
}

public class CollectionPolicyListRequestDto : PagedAndSortedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public string? Status { get; set; }
	public bool IncludeArchived { get; set; }
	public string? Search { get; set; }
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
