namespace ShikshaMitra.Finance.Fees.Concessions;

public enum ConcessionRequestStatus
{
	Pending = 0,
	Approved = 1,
	Rejected = 2,
	Cancelled = 3,
	PartiallyReversed = 4,
	Reversed = 5,
	// Added without renumbering historic persisted workflow values.
	Draft = 6,
}
