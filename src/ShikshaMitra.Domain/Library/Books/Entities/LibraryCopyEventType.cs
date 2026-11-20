namespace ShikshaMitra.Library.Books;

public enum LibraryCopyEventType
{
	ConditionChanged = 0,
	Recovered = 1,
	NotesChanged = 2,
	Moved = 3,
	WrittenOff = 4,
	Issued = 5,
	Returned = 6,
	HoldAllocated = 7,
	HoldReleased = 8,
	CustodyHandedOff = 9,
	CustodyReceived = 10,
	TransferDispatched = 11,
	TransferReceived = 12
}
