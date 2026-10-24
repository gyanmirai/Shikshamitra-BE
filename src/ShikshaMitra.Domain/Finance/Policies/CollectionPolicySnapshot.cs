using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// Immutable calculation payload stored in FinancePolicyVersion. It deliberately contains
/// no audit/UI-only fields so a historical student assignment can be calculated without
/// reading the mutable CollectionPolicy row.
/// </summary>
public sealed class CollectionPolicySnapshot
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		Converters = { new JsonStringEnumConverter() }
	};

	public string PolicyName { get; set; } = string.Empty;
	public string PolicyCode { get; set; } = string.Empty;
	public string? Description { get; set; }
	public CollectionPolicyStatus Status { get; set; }
	public bool IsDefault { get; set; }
	public DateTime? EffectiveFrom { get; set; }
	public DateTime? EffectiveTo { get; set; }
	public bool EnableLateFee { get; set; }
	public int GracePeriodDays { get; set; }
	public List<Guid> LateFeeApplyOnFeeHeads { get; set; } = [];
	public LateFeeCalculationType LateFeeType { get; set; }
	public decimal? FlatAmount { get; set; }
	public decimal? Percentage { get; set; }
	public PercentageBaseType PercentageBase { get; set; }
	public decimal? AmountPerDay { get; set; }
	public decimal? AmountPerWeek { get; set; }
	public decimal? AmountPerMonth { get; set; }
	public CompoundFrequencyType CompoundFrequency { get; set; }
	public List<CollectionPolicyEscalationRule> EscalationRules { get; set; } = [];
	public List<CollectionPolicyFeeHeadOverride> FeeHeadOverrides { get; set; } = [];
	public RoundingMethodType RoundingMethod { get; set; }
	public bool ConsiderAsIncome { get; set; }
	public Guid? LateFeeIncomeHeadId { get; set; }
	public Guid? LateFeeIncomeCategoryId { get; set; }
	public decimal? MaximumLateFeeAmount { get; set; }
	public int? MaximumChargingDays { get; set; }
	public bool EnableWaiver { get; set; }
	public bool EnableChargeWaiver { get; set; }
	public bool RequiresApproval { get; set; }
	public List<Guid> ApprovalRoleIds { get; set; } = [];
	public bool AllowOnlineCollection { get; set; }
	public CollectionAllocationBasis AllocationBasis { get; set; } = CollectionAllocationBasis.Legacy;

	public CollectionReceiptGrouping ReceiptGrouping { get; set; } = CollectionReceiptGrouping.Separate;

	public string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

	public static CollectionPolicySnapshot Deserialize(string json) =>
		JsonSerializer.Deserialize<CollectionPolicySnapshot>(json, JsonOptions)
		?? throw new JsonException("Collection Policy version snapshot is empty.");

	public CollectionPolicyUpdateModel ToUpdateModel() => new(
		PolicyName, PolicyCode, Description, Status, IsDefault, EffectiveFrom, EffectiveTo,
		EnableLateFee, GracePeriodDays, LateFeeApplyOnFeeHeads, LateFeeType,
		FlatAmount, Percentage, PercentageBase, AmountPerDay, AmountPerWeek, AmountPerMonth,
		CompoundFrequency, EscalationRules, FeeHeadOverrides, RoundingMethod, ConsiderAsIncome,
		LateFeeIncomeHeadId, MaximumLateFeeAmount, MaximumChargingDays, EnableWaiver,
		EnableChargeWaiver, RequiresApproval, ApprovalRoleIds, AllowOnlineCollection, LateFeeIncomeCategoryId, AllocationBasis, ReceiptGrouping);
}
