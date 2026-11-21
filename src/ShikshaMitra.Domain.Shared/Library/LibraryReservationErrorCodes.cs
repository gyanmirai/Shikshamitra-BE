namespace ShikshaMitra.Library;

public static class LibraryReservationErrorCodes
{
	public const string InvalidRequest = "Library:Reservations:010100";
	public const string Conflict = "Library:Reservations:010101";
	public const string PriorityBlocked = "Library:Reservations:010102";
	public const string ReviewRequired = "Library:Reservations:010103";
	public const string HoldNotFound = "Library:Reservations:010104";
	public const string InvalidTransition = "Library:Reservations:010105";
	public const string InvalidQueue = "Library:Reservations:010106";
}
