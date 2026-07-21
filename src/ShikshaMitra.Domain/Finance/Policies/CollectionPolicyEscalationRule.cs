namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>
/// A single late-fee escalation slab. Persisted as part of CollectionPolicy's
/// EscalationRulesJson column — slabs have no independent lifecycle or query
/// need outside their owning policy, so a child aggregate/table is unnecessary.
/// </summary>
public sealed record CollectionPolicyEscalationRule(
	int StartDay,
	int? EndDay,
	EscalationCalculationType CalculationType,
	decimal Value,
	decimal? MaxFee);
