using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public class LibraryReservationNotificationEvidenceReader(
	IRepository<BookReservation, Guid> requests, IRepository<LibraryHold, Guid> holds,
	IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	IRepository<BookIssue, Guid> issues, IAsyncQueryableExecuter query, ICurrentTenant tenant) : ITransientDependency
{
	public async Task<LibraryReservationNotificationEvidence?> ReadAsync(Guid requestId, string eventType, DateOnly? today)
	{
		if (!tenant.Id.HasValue) return null;
		var request = await requests.FindAsync(row => row.Id == requestId && row.TenantId == tenant.Id);
		if (request == null || !request.CanonicalMemberId.HasValue || request.CanonicalMemberId == Guid.Empty ||
			!request.AcademicYearId.HasValue || request.MemberRole is not (0 or 1) || !request.SchoolRequestedDate.HasValue || !Confirmed(request.RequestedDateProvenance) ||
			request.State is not (LibraryReservationState.Waiting or LibraryReservationState.Ready)) return null;
		var book = await books.FindAsync(row => row.Id == request.BookId && row.TenantId == tenant.Id);
		if (book == null) return null;
		LibraryHold? hold = null;
		BookCopy? copy = null;
		DateOnly? deadline = null;
		if (request.State == LibraryReservationState.Ready)
		{
			var active = await query.ToListAsync((await holds.GetQueryableAsync()).Where(row => row.TenantId == tenant.Id &&
				row.ReservationId == request.Id && row.BookId == request.BookId && row.State == LibraryHoldState.Allocated).Take(2));
			if (active.Count != 1) return null;
			hold = active[0];
			if (hold.RelatedLoanId.HasValue || (hold.SchoolPickupDeadline.HasValue && !Confirmed(hold.PickupDeadlineProvenance))) return null;
			copy = await copies.FindAsync(row => row.Id == hold.CopyId && row.BookId == request.BookId && row.TenantId == tenant.Id);
			if (copy == null || copy.Status != BookCopyStatus.Reserved) return null;
			var accession = copy.AccessionNumber.Trim().ToUpperInvariant();
			if (await issues.AnyAsync(loan => loan.TenantId == tenant.Id && loan.Status == BookIssueStatus.Issued &&
				(loan.BookCopyId == copy.Id || loan.AccessionNumber.Trim().ToUpper() == accession))) return null;
			deadline = hold.SchoolPickupDeadline;
		}
		else if (request.SchoolRequestExpiryDate.HasValue)
		{
			if (!Confirmed(request.RequestExpiryDateProvenance)) return null;
			deadline = request.SchoolRequestExpiryDate;
		}
		if (eventType == LibraryNotificationEventTypes.ReservedBookAvailable && hold == null) return null;
		if (eventType == LibraryNotificationEventTypes.ReservationExpiring && !deadline.HasValue) return null;
		if (today.HasValue && deadline.HasValue && deadline.Value < today.Value) return null;
		var signature = JsonSerializer.Serialize(new {
			request.State, request.Revision, request.CanonicalMemberId, request.BookId, request.AcademicYearId,
			request.BorrowerBoardId, request.BorrowerMediumId, request.BorrowerStageId, request.BorrowerStandardId,
			request.BorrowerDivisionId, request.BorrowerScopeProvenance, request.BorrowerScopeSnapshotJson,
			request.SchoolRequestedDate, request.RequestedDateProvenance, request.SchoolRequestExpiryDate, request.RequestExpiryDateProvenance,
			HoldId = hold?.Id, HoldState = hold?.State, CopyId = hold?.CopyId, PickupDeadline = hold?.SchoolPickupDeadline,
			PickupProvenance = hold?.PickupDeadlineProvenance, TimeZoneId = hold?.TimeZoneId,
			AllocatedAt = hold?.AllocatedAt, RelatedLoanId = hold?.RelatedLoanId, CopyStatus = copy?.Status,
			AccessionNumber = copy?.AccessionNumber, SectionId = copy?.SectionId, ShelfId = copy?.ShelfId
		});
		return new(request, request.CanonicalMemberId.Value, book, hold, copy, deadline, signature);
	}
	private static bool Confirmed(LibraryCivilDateProvenance value) => value is LibraryCivilDateProvenance.ExplicitSchoolDate or LibraryCivilDateProvenance.ReviewedLegacy;
}
