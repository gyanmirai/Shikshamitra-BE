namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>Fee-head selection belongs to a fee-structure policy attachment, not to the reusable policy.</summary>
public enum ConcessionFeeHeadScope
{
	EntireFeeStructure = 0,
	SelectedFeeHeads = 1,
}
