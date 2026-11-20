using Volo.Abp;

namespace ShikshaMitra.Library.Policies;

public sealed record LibraryBorrowingTerms(
	int MaxBooksAtTime, int ReservationLimit, int LoanDays, LibraryDayCountingMode DayCountingMode,
	int MaxRenewals, int RenewalDays, int PickupWindowDays, bool MoveDueDateToNextOpenDay,
	LibraryDueDateMode DueDateMode = LibraryDueDateMode.Automatic)
{
	public LibraryBorrowingTerms Validated()
	{
		if (!System.Enum.IsDefined(DueDateMode) || MaxBooksAtTime is < 0 or > 1000 || ReservationLimit is < 0 or > 1000 ||
			LoanDays is < 0 or > 3660 || DueDateMode == LibraryDueDateMode.Automatic && LoanDays == 0 ||
			DayCountingMode is not (LibraryDayCountingMode.CalendarDays or LibraryDayCountingMode.OpenDays) ||
			MaxRenewals is < 0 or > 100 || RenewalDays is < 0 or > 3660 ||
			DueDateMode == LibraryDueDateMode.Automatic && MaxRenewals > 0 && RenewalDays == 0 ||
			PickupWindowDays is < 0 or > 365 || DueDateMode == LibraryDueDateMode.Automatic && PickupWindowDays == 0)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "Borrowing durations and limits must be valid before a policy can be saved.");
		return this;
	}
}
