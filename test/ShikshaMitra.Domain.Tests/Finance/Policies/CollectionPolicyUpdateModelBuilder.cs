using System;
using System.Collections.Generic;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Mutable-property builder for CollectionPolicyUpdateModel so calculation-service tests
/// can set only the fields relevant to the scenario under test rather than filling out
/// every one of the record's 30 positional parameters each time.
/// </summary>
public sealed class CollectionPolicyUpdateModelBuilder
{
	public CollectionPolicyUpdateModelBuilder()
	{
		var defaultFeeHeadId = Guid.NewGuid();
		LateFeeApplyOnFeeHeads = new[] { defaultFeeHeadId };
		WaiverApplyOnFeeHeads = new[] { defaultFeeHeadId };
		ApprovalRoleIds = new[] { Guid.NewGuid() };
	}

	public string PolicyName { get; set; } = "Test Policy";
	public string PolicyCode { get; set; } = "CP-TEST";
	public string? Description { get; set; }
	public CollectionPolicyStatus Status { get; set; } = CollectionPolicyStatus.Active;
	public bool IsDefault { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool EnableLateFee { get; set; } = true;
	public int GracePeriodDays { get; set; }
	public GracePeriodType GracePeriodType { get; set; } = GracePeriodType.Calendar;
	public IReadOnlyList<Guid> LateFeeApplyOnFeeHeads { get; set; } = Array.Empty<Guid>();
	public LateFeeCalculationType LateFeeType { get; set; } = LateFeeCalculationType.Flat;
	public decimal? FlatAmount { get; set; } = 10;
	public decimal? Percentage { get; set; }
	public PercentageBaseType PercentageBase { get; set; } = PercentageBaseType.OutstandingAmount;
	public decimal? AmountPerDay { get; set; }
	public decimal? AmountPerWeek { get; set; }
	public decimal? AmountPerMonth { get; set; }
	public CompoundFrequencyType CompoundFrequency { get; set; } = CompoundFrequencyType.Monthly;
	public IReadOnlyList<CollectionPolicyEscalationRule> EscalationRules { get; set; } = Array.Empty<CollectionPolicyEscalationRule>();
	public IReadOnlyList<CollectionPolicyFeeHeadOverride> FeeHeadOverrides { get; set; } = Array.Empty<CollectionPolicyFeeHeadOverride>();
	public RoundingMethodType RoundingMethod { get; set; } = RoundingMethodType.None;
	public bool ConsiderAsIncome { get; set; }
	public Guid? LateFeeIncomeHeadId { get; set; }
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }
	public bool EnableWaiver { get; set; } = true;
	public IReadOnlyList<Guid> WaiverApplyOnFeeHeads { get; set; } = Array.Empty<Guid>();
	public decimal? MaximumWaiverAmount { get; set; }
	public decimal? MaximumWaiverPercentage { get; set; }
	public DateTime? WaiverExpiry { get; set; }
	public bool RequiresApproval { get; set; }
	public IReadOnlyList<Guid> ApprovalRoleIds { get; set; } = Array.Empty<Guid>();
	public decimal? ApprovalThresholdAmount { get; set; }
	public bool AllowOnlineCollection { get; set; } = true;

	public CollectionPolicyUpdateModel Build() => new(
		PolicyName, PolicyCode, Description, Status, IsDefault, EffectiveFrom, EffectiveTo,
		EnableLateFee, GracePeriodDays, GracePeriodType, LateFeeApplyOnFeeHeads, LateFeeType,
		FlatAmount, Percentage, PercentageBase, AmountPerDay, AmountPerWeek, AmountPerMonth,
		CompoundFrequency, EscalationRules, FeeHeadOverrides, RoundingMethod, ConsiderAsIncome, LateFeeIncomeHeadId,
		MaximumLateFeeAmount, MaximumChargingDays, EnableWaiver, WaiverApplyOnFeeHeads,
		MaximumWaiverAmount, MaximumWaiverPercentage, WaiverExpiry, RequiresApproval,
		ApprovalRoleIds, ApprovalThresholdAmount, AllowOnlineCollection);
}
