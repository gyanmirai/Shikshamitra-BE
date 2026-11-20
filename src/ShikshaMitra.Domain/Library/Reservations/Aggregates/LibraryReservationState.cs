namespace ShikshaMitra.Library.Reservations;

public enum LibraryReservationState
{
	Waiting = 0,
	Ready = 1,
	Fulfilled = 2,
	Cancelled = 3,
	Expired = 4,
	NeedsReview = 5
}

public enum LibraryHoldState
{
	Allocated = 0,
	Collected = 1,
	Released = 2,
	Expired = 3
}

public enum LibraryReservationEligibility
{
	Eligible = 0,
	PermanentlyIneligible = 1,
	NeedsReview = 2
}

public enum LibraryReservationQueueProvenance
{
	UnconfirmedLegacy = 0,
	ServerSequence = 1,
	ReviewedLegacy = 2
}
