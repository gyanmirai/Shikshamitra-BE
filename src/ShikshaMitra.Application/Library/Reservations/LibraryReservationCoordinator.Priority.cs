using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;

namespace ShikshaMitra.Library.Reservations;

public partial class LibraryReservationCoordinator
{
	public async Task<LibraryIssuePriorityResult> ReadIssuePriorityAsync(BookCopy copy, Guid canonicalMemberId)
	{
		var view = await LoadAsync(copy.BookId);
		var actual = view.Copies.SingleOrDefault(row => row.Id == copy.Id);
		var settings = await calendar.GetSettingsAsync();
		var calendarClosures = await closures.GetListAsync(row => row.TenantId == CurrentTenant.Id);
		var data = JsonSerializer.Serialize(new
		{
			copy.BookId, view.Book.CategoryId, CopyId = copy.Id, canonicalMemberId, view.Today, settings.TimeZoneId, settings.Source, settings.NeedsConfiguration,
			Requests = view.Requests.OrderBy(row => row.Id).Select(row => new { row.Id, row.State, row.QueueSequence, row.Revision, row.CanonicalMemberId, row.PickupSectionId }),
			Holds = view.Holds.OrderBy(row => row.Id).Select(row => new { row.Id, row.ReservationId, row.CopyId, row.State,
				row.SchoolPickupDeadline, row.PickupDeadlineProvenance, row.PolicySnapshotJson, row.CalendarSnapshotJson }),
			Copies = view.Copies.OrderBy(row => row.Id).Select(row => new { row.Id, row.CopyNumber, row.Status, row.SectionId, row.ShelfId, row.Remarks }),
			People = view.People.Values.DistinctBy(row => row.PersonId).OrderBy(row => row.PersonId).Select(row => new
			{
				row.PersonId, row.Role, row.AcademicYearId, row.StageId, row.StandardId, row.Eligibility, MemberIds = row.MemberIds.OrderBy(id => id)
			}),
			OpenLoanCopyIds = view.LoanCopyIds.OrderBy(id => id),
			Closures = calendarClosures.OrderBy(row => row.Id).Select(row => new { row.Id, row.FromDate, row.ToDate, row.SectionId })
		});
		var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
		LibraryIssuePriorityResult Result(bool allowed, string? reason = null) => new(allowed, reason, revision);
		if (actual == null || view.LoanCopyIds.Contains(copy.Id)) return Result(false, "This copy is unavailable or has an unresolved loan.");
		var hold = view.Holds.SingleOrDefault(row => row.CopyId == copy.Id);
		var heldRequest = hold == null ? null : view.Requests.SingleOrDefault(row => row.Id == hold.ReservationId);
		var released = hold != null && heldRequest != null && (Expired(heldRequest, hold, view.Today) ||
			Facts(view, heldRequest)?.Eligibility == LibraryReservationEligibility.PermanentlyIneligible);
		if (hold != null && !released)
		{
			var allowed = heldRequest != null && heldRequest.State == LibraryReservationState.Ready &&
				heldRequest.CanonicalMemberId == canonicalMemberId && !RequiresReview(heldRequest, Facts(view, heldRequest), view.YearIds) && !InvalidReady(view, heldRequest) &&
				actual.Status == BookCopyStatus.Reserved;
			return Result(allowed, allowed ? null : "This copy is allocated to a reservation or requires review.");
		}
		if (actual.Status != BookCopyStatus.Available && !(released && actual.Status == BookCopyStatus.Reserved))
			return Result(false, "This copy is not available for issue.");
		var effectiveRequests = view.Requests.Where(row => !Expired(row, view.Holds.SingleOrDefault(item => item.ReservationId == row.Id), view.Today) &&
			!(row.State == LibraryReservationState.Ready && Facts(view, row)?.Eligibility == LibraryReservationEligibility.PermanentlyIneligible)).ToArray();
		var effectiveHolds = view.Holds.Where(row => effectiveRequests.Any(request => request.Id == row.ReservationId)).ToArray();
		var effectiveCopies = view.Copies.Where(row => !view.LoanCopyIds.Contains(row.Id) &&
			(row.Status == BookCopyStatus.Available || row.Status == BookCopyStatus.Reserved && view.Holds.Any(item => item.CopyId == row.Id) &&
				!effectiveHolds.Any(item => item.CopyId == row.Id))).Where(row => !effectiveHolds.Any(item => item.CopyId == row.Id))
			.Select(row => new LibraryReservationAvailableCopy(row.Id, row.CopyNumber, row.SectionId)).ToArray();
		var queue = effectiveRequests.Select(row => new LibraryReservationQueueEntry(row.Id, row.QueueSequence, row.State,
			RequiresReview(row, Facts(view, row), view.YearIds) || InvalidReady(view, row) || row.State == LibraryReservationState.Waiting &&
				(settings.Source != "Library" || settings.NeedsConfiguration) &&
				!(JsonSerializer.Deserialize<LibraryBorrowingPolicySnapshotDto>(row.PolicySnapshotJson ?? "null", SnapshotJsonOptions) is { } saved && IsManual(saved))
				? LibraryReservationEligibility.NeedsReview : Facts(view, row)!.Eligibility, PickupSectionId: row.PickupSectionId)).ToArray();
		var plan = LibraryFifoAllocationPlanner.Plan(queue, effectiveCopies);
		var assigned = plan.Allocations.SingleOrDefault(row => row.CopyId == copy.Id);
		if (assigned != null)
		{
			var allowed = effectiveRequests.Single(row => row.Id == assigned.ReservationId).CanonicalMemberId == canonicalMemberId;
			return Result(allowed, allowed ? null : "Earlier reservation priority must be satisfied before this copy can be issued.");
		}
		return plan.BlocksAt(actual.SectionId) ? Result(false, "Reservation priority requires review before allocating another copy.") : Result(true);
	}
}
