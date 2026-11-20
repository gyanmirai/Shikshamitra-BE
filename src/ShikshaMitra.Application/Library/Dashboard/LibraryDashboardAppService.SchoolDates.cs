using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;

namespace ShikshaMitra.Library.Dashboard;

public partial class LibraryDashboardAppService
{
	private async Task<List<LibraryHold>> GetCollectableHoldsAsync(IReadOnlyCollection<BookReservation> requests)
	{
		var ids = requests.Where(row => row.State == LibraryReservationState.Ready).Select(row => row.Id).ToArray();
		if (ids.Length == 0) return [];
		var copies = await bookCopyRepository.GetQueryableAsync();
		var issues = await issueRepository.GetQueryableAsync();
		var holds = (await holdRepository.GetQueryableAsync()).Where(hold => hold.TenantId == CurrentTenant.Id &&
			Enumerable.Contains(ids, hold.ReservationId) && hold.State == LibraryHoldState.Allocated &&
			hold.EndedAt == null && hold.RelatedLoanId == null && copies.Any(copy => copy.Id == hold.CopyId &&
				copy.BookId == hold.BookId && copy.TenantId == CurrentTenant.Id && copy.Status == BookCopyStatus.Reserved &&
				!issues.Any(loan => loan.TenantId == CurrentTenant.Id && loan.Status == BookIssueStatus.Issued &&
					(loan.BookCopyId == copy.Id || loan.AccessionNumber.Trim().ToUpper() == copy.NormalizedAccessionNumber))));
		return await AsyncExecuter.ToListAsync(holds);
	}

	private static DateOnly? RequestDeadline(BookReservation request, IReadOnlyCollection<LibraryHold> holds)
	{
		if (request.State == LibraryReservationState.Waiting)
			return LibraryLoanDates.IsConfirmed(request.RequestExpiryDateProvenance) ? request.SchoolRequestExpiryDate : null;
		if (request.State != LibraryReservationState.Ready) return null;
		var active = holds.Where(hold => hold.ReservationId == request.Id && hold.BookId == request.BookId).Take(2).ToArray();
		return active.Length == 1 && LibraryLoanDates.IsConfirmed(active[0].PickupDeadlineProvenance) ? active[0].SchoolPickupDeadline : null;
	}
}
