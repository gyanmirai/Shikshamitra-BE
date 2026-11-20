namespace ShikshaMitra.Library.InventoryVerification;

public enum LibraryStockFinding
{
	Observed = 1,
	ObservedElsewhere = 2,
	ObservedLocationUnconfirmed = 3,
	ObservedWithUnavailableStatus = 4,
	OnLoan = 5,
	OnHold = 6,
	InRepair = 7,
	RecordedLost = 8,
	Withdrawn = 9,
	NotSeen = 10,
	MovedOutsideScope = 11,
	RegisterConflict = 12,
	CurrentCopyUnavailable = 13,
	OutsideBaseline = 14,
	UnknownAccession = 15,
	InCustody = 16,
	InTransit = 17
}

public enum LibraryStockReconciliationSource
{
	Expected = 1,
	OutsideBaseline = 2,
	UnknownScans = 3
}
