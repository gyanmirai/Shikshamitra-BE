using System;
using System.Collections.Generic;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// How a fee head deviates from the policy-level late-fee rule — Phase 4a
/// (collection-policy-implementation-plan.md §4a).
/// </summary>
public enum FeeHeadOverrideMode
{
	/// <summary>Fee head uses the policy-level LateFeeRule unchanged (the default for every fee head).</summary>
	Inherit = 0,
	/// <summary>Fee head never accrues a late fee, regardless of the policy-level rule.</summary>
	NoLateFee = 1,
	/// <summary>Fee head uses its own LateFeeRule (<see cref="CollectionPolicyFeeHeadOverride.LateFeeRule"/>) instead of the policy-level one.</summary>
	CustomRule = 2
}

/// <summary>
/// A single fee head's deviation from the policy-level late-fee rule. Persisted as part of
/// CollectionPolicy's FeeHeadOverridesJson column — overrides have no independent lifecycle
/// or query need outside their owning policy, same rationale as CollectionPolicyEscalationRule.
/// </summary>
public sealed record CollectionPolicyFeeHeadOverride(
	Guid FeeHeadId,
	FeeHeadOverrideMode OverrideMode,
	LateFeeRuleOverride? LateFeeRule);

/// <summary>
/// The subset of CollectionPolicy's late-fee fields that make sense to override per fee head —
/// mirrors the policy-level shape (implementation plan §4a: "an optional nested LateFeeRule,
/// same shape as the policy-level one"). Only populated when OverrideMode = CustomRule.
/// </summary>
public sealed record LateFeeRuleOverride(
	LateFeeCalculationType LateFeeType,
	decimal? FlatAmount,
	decimal? Percentage,
	PercentageBaseType PercentageBase,
	decimal? AmountPerDay,
	decimal? AmountPerWeek,
	decimal? AmountPerMonth,
	CompoundFrequencyType CompoundFrequency,
	IReadOnlyList<CollectionPolicyEscalationRule> EscalationRules,
	RoundingMethodType RoundingMethod,
	decimal? MaximumLateFeeAmount,
	int? MaximumChargingDays);
