namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>Legacy preserves existing assignments; configured modes follow the collection ledger order.</summary>
public enum CollectionAllocationBasis
{
	Legacy = 0,
	Installment = 1,
	FeeHead = 2
}

public enum CollectionPolicyStatus
{
	Active = 0,
	Archived = 1,
	// Keep the existing numeric values stable for deployed rows. New policies use Draft.
	Draft = 2
}

public enum LateFeeCalculationType
{
	Flat = 0,
	Percentage = 1,
	PerDay = 2,
	PerWeek = 3,
	PerMonth = 4,
	Compound = 5,
	Slab = 6
}

public enum PercentageBaseType
{
	OutstandingAmount = 0,
	OriginalFee = 1,
	RemainingBalance = 2
}

public enum CompoundFrequencyType
{
	Daily = 0,
	Weekly = 1,
	Monthly = 2
}

public enum RoundingMethodType
{
	None = 0,
	RoundUp = 1,
	RoundDown = 2,
	NearestRupee = 3
}

public enum EscalationCalculationType
{
	Flat = 0,
	Percentage = 1
}

/// <summary>
/// WorkingDay counting requires a holiday-calendar source that doesn't exist yet for
/// Collection Policy (see collection-policy-implementation-plan.md Open Questions) — the
/// field is modeled now so the schema/UI can capture the school's intent, but
/// LateFeeCalculationService currently treats WorkingDay identically to Calendar until
/// that dependency is resolved.
/// </summary>
public enum GracePeriodType
{
	Calendar = 0,
	WorkingDay = 1
}
