using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Reservations;

public partial class LibraryReservationCoordinator
{
	public async Task RebalanceAsync(Guid bookId, DateOnly? schoolDate = null)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var view = await LoadAsync(bookId);
		var system = LibraryOperationActor.System(LibrarySystemPurpose.HoldAllocator);
		foreach (var request in view.Requests.Where(row => Active(row.State)).ToList())
		{
			var hold = view.Holds.SingleOrDefault(row => row.ReservationId == request.Id && row.State == LibraryHoldState.Allocated);
			if (Expired(request, hold, view.Today))
			{
				await EndAsync(request, LibraryReservationState.Expired, LibraryHoldState.Expired, "The confirmed school pickup or request deadline elapsed.",
					LibraryOperationActor.System(LibrarySystemPurpose.HoldExpiry));
				continue;
			}
			var facts = Facts(view, request);
			if (facts?.Eligibility == LibraryReservationEligibility.PermanentlyIneligible)
			{
				await EndAsync(request, LibraryReservationState.Cancelled, LibraryHoldState.Released,
					facts.Reason ?? "Library eligibility ended.", system);
				continue;
			}
			if (RequiresReview(request, facts, view.YearIds) || InvalidReady(view, request)) await MarkReviewAsync(request, facts?.Reason ?? "Review the reservation's member, dates, academic context, and allocated copy.");
		}
		view = await LoadAsync(bookId);
		var allocationDate = view.Today ?? schoolDate;
		var deferred = new HashSet<Guid>();
		while (await AllocateWaitingAsync(view, allocationDate, system, deferred) is Guid blocked)
			deferred.Add(blocked);
	}

	private async Task<Guid?> AllocateWaitingAsync(AllocationView view, DateOnly? allocationDate,
		LibraryOperationActor system, HashSet<Guid> deferred)
	{
		var queue = Queue(view).Select(row => deferred.Contains(row.ReservationId)
			? row with { Eligibility = LibraryReservationEligibility.NeedsReview }
			: row).ToArray();
		var plan = LibraryFifoAllocationPlanner.Plan(queue, Available(view));
		foreach (var allocation in plan.Allocations)
		{
			var request = view.Requests.Single(row => row.Id == allocation.ReservationId);
			var copy = view.Copies.Single(row => row.Id == allocation.CopyId);
			var facts = Facts(view, request)!;
			var saved = JsonSerializer.Deserialize<LibraryBorrowingPolicySnapshotDto>(request.PolicySnapshotJson ?? "null", SnapshotJsonOptions);
			if (saved == null || !IsManual(saved) && !allocationDate.HasValue) break;
			var policy = IsManual(saved) ? saved :
				await ResolvePolicyAsync(view.Book, copy.SectionId, facts, request.AcademicYearId!.Value, allocationDate!.Value);
			var deadline = request.SchoolRequestExpiryDate;
			var zone = (string?)null;
			var calendarSnapshot = JsonSerializer.Serialize(new { Source = "ExplicitManualDate", request.SchoolRequestExpiryDate, request.RequestExpiryDateProvenance });
			if (!IsManual(policy))
			{
				if (!allocationDate.HasValue) return request.Id;
				var settings = await calendar.GetSettingsAsync();
				if (settings.Source != "Library" || settings.NeedsConfiguration) return request.Id;
				try
				{
					var calculation = await calendar.CalculateAsync(new LibraryCalendarCalculateRequest
					{
						AcademicYearId = request.AcademicYearId!.Value, SectionId = copy.SectionId, StartDate = allocationDate.Value,
						Days = policy.Terms.PickupWindowDays!.Value, DayCountingMode = policy.Terms.DayCountingMode!.Value,
						MoveToNextOpenDay = policy.Terms.MoveDueDateToNextOpenDay ?? true
					});
					deadline = !deadline.HasValue || calculation.DueDate < deadline.Value ? calculation.DueDate : deadline;
					zone = calculation.TimeZoneId;
					var ranges = await closures.GetListAsync(row => row.TenantId == CurrentTenant.Id && (row.SectionId == null || row.SectionId == copy.SectionId));
					calendarSnapshot = JsonSerializer.Serialize(new { calculation, AllocationDate = allocationDate,
						request.AcademicYearId, copy.SectionId, OpenWeekdays = await calendar.GetOpenWeekdaysAsync(request.AcademicYearId.Value),
						Closures = ranges.OrderBy(row => row.Id).Select(row => new { row.Id, row.FromDate, row.ToDate, row.SectionId }) });
				}
				catch (BusinessException error) when (error.Code is LibraryErrorCodes.CalendarNoOpenDay
					or LibraryErrorCodes.CalendarNotConfigured or LibraryErrorCodes.CalendarInvalid)
				{
					// Keep the real request waiting with its original queue position until the calendar permits allocation.
					return request.Id;
				}
			}
			if (!deadline.HasValue)
			{
				await MarkReviewAsync(request, "Review the pickup deadline before allocating a copy.");
				break;
			}
			var before = LibraryReservationManager.Capture(request);
			var hold = new LibraryHold(GuidGenerator.Create(), CurrentTenant.Id, request.Id, view.Book.Id, copy.Id, Now, deadline,
				request.RequestExpiryDateProvenance == LibraryCivilDateProvenance.ReviewedLegacy
					? LibraryCivilDateProvenance.ReviewedLegacy : LibraryCivilDateProvenance.ExplicitSchoolDate,
				zone, JsonSerializer.Serialize(policy), calendarSnapshot);
			await holds.InsertAsync(hold, true);
			view.Holds.Add(hold);
			var copyBefore = LibraryCopySnapshot.Capture(copy);
			copy.Status = BookCopyStatus.Reserved;
			await copyEvents.RecordAsync(copyBefore, copy, LibraryCopyEventType.HoldAllocated, system, "Allocated to the next eligible reservation in server queue order.");
			await copies.UpdateAsync(copy, true);
			request.Ready();
			await requests.UpdateAsync(request, true);
			await history.RecordAsync(request, LibraryReservationState.Waiting, before, system, "Allocated the next available copy.", "Ready", hold);
			await notificationSink.NotifyAllocatedAsync(request, hold, view.Book, copy);
		}
		return null;
	}

	private async Task EndAsync(BookReservation request, LibraryReservationState next, LibraryHoldState holdState,
		string reason, LibraryOperationActor actor)
	{
		var hold = await holds.FindAsync(row => row.ReservationId == request.Id && row.TenantId == CurrentTenant.Id && row.State == LibraryHoldState.Allocated);
		var before = LibraryReservationManager.Capture(request);
		var previous = request.State;
		if (hold != null)
		{
			var copy = await copies.GetAsync(hold.CopyId);
			if (copy.BookId != request.BookId || await HasOpenLoanAsync(copy))
			{
				await MarkReviewAsync(request, "An allocated hold conflicts with the physical copy or an unresolved loan. Review its history.");
				return;
			}
			hold.Finish(holdState, reason, Now);
			await holds.UpdateAsync(hold, true);
			if (copy.Status == BookCopyStatus.Reserved)
			{
				var copyBefore = LibraryCopySnapshot.Capture(copy);
				copy.Status = BookCopyStatus.Available;
				await copyEvents.RecordAsync(copyBefore, copy, LibraryCopyEventType.HoldReleased, actor, reason);
				await copies.UpdateAsync(copy, true);
			}
		}
		request.Finish(next, reason, Now);
		await requests.UpdateAsync(request, true);
		await history.RecordAsync(request, previous, before, actor, reason, next.ToString(), hold);
	}

	private async Task MarkReviewAsync(BookReservation request, string reason)
	{
		if (request.State == LibraryReservationState.NeedsReview) return;
		var before = LibraryReservationManager.Capture(request);
		var previous = request.State;
		request.RequireReview(reason);
		await requests.UpdateAsync(request, true);
		await history.RecordAsync(request, previous, before, LibraryOperationActor.System(LibrarySystemPurpose.HoldAllocator), reason, "ReviewRequired");
	}

	private Task<bool> HasOpenLoanAsync(BookCopy copy) => loans.AnyAsync(row => row.TenantId == CurrentTenant.Id && row.Status == BookIssueStatus.Issued &&
		(row.BookCopyId == copy.Id || row.AccessionNumber.Trim().ToUpper() == copy.NormalizedAccessionNumber));

	private static bool Expired(BookReservation request, LibraryHold? hold, DateOnly? today)
	{
		if (!today.HasValue) return false;
		if (request.State == LibraryReservationState.Ready)
			return hold?.SchoolPickupDeadline is { } due && hold.PickupDeadlineProvenance != LibraryCivilDateProvenance.UnconfirmedStoredComponent && due < today.Value;
		return request.State == LibraryReservationState.Waiting && request.SchoolRequestExpiryDate.HasValue &&
			request.RequestExpiryDateProvenance != LibraryCivilDateProvenance.UnconfirmedStoredComponent && request.SchoolRequestExpiryDate.Value < today.Value;
	}

	private static LibraryReservationBorrowerFacts? Facts(AllocationView view, BookReservation request) =>
		view.People.GetValueOrDefault(request.CanonicalMemberId ?? request.MemberId);
	private static bool InvalidReady(AllocationView view, BookReservation request)
	{
		if (request.State != LibraryReservationState.Ready) return false;
		var hold = view.Holds.SingleOrDefault(row => row.ReservationId == request.Id);
		return hold == null || !hold.SchoolPickupDeadline.HasValue || hold.PickupDeadlineProvenance == LibraryCivilDateProvenance.UnconfirmedStoredComponent ||
			view.LoanCopyIds.Contains(hold.CopyId) || !view.Copies.Any(row => row.Id == hold.CopyId && row.BookId == request.BookId &&
				row.Status == BookCopyStatus.Reserved && (!request.PickupSectionId.HasValue || row.SectionId == request.PickupSectionId));
	}
	private static bool RequiresReview(BookReservation request, LibraryReservationBorrowerFacts? person, IReadOnlySet<Guid> years) =>
		request.State == LibraryReservationState.NeedsReview || request.CanonicalMemberId == null || request.QueueSequence == null ||
		request.AcademicYearId == null || !years.Contains(request.AcademicYearId.Value) || request.SchoolRequestedDate == null ||
		request.RequestedDateProvenance == LibraryCivilDateProvenance.UnconfirmedStoredComponent || person == null ||
		person.Eligibility == LibraryReservationEligibility.NeedsReview || person.Role != request.MemberRole ||
		person.Role == 0 && person.AcademicYearId != request.AcademicYearId;

	private static LibraryReservationQueueEntry[] Queue(AllocationView view) => view.Requests.Where(row => Active(row.State))
		.Select(row => new LibraryReservationQueueEntry(row.Id, row.QueueSequence, row.State,
			RequiresReview(row, Facts(view, row), view.YearIds) || InvalidReady(view, row) ? LibraryReservationEligibility.NeedsReview : Facts(view, row)!.Eligibility,
			PickupSectionId: row.PickupSectionId)).ToArray();
	private static LibraryReservationAvailableCopy[] Available(AllocationView view) => view.Copies.Where(row =>
		row.Status == BookCopyStatus.Available && !view.Holds.Any(hold => hold.CopyId == row.Id && hold.State == LibraryHoldState.Allocated) &&
		!view.LoanCopyIds.Contains(row.Id)).Select(row => new LibraryReservationAvailableCopy(row.Id, row.CopyNumber, row.SectionId)).ToArray();

	private async Task<AllocationView> LoadAsync(Guid bookId)
	{
		var book = await books.GetAsync(bookId);
		var requestRows = await requests.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.BookId == bookId &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview));
		var holdRows = await holds.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.BookId == bookId && row.State == LibraryHoldState.Allocated);
		var copyRows = await copies.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.BookId == bookId);
		var ids = copyRows.Select(row => row.Id).ToArray();
		var accessions = copyRows.Select(row => row.NormalizedAccessionNumber).ToArray();
		var open = await AsyncExecuter.ToListAsync((await loans.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			row.Status == BookIssueStatus.Issued && (Enumerable.Contains(ids, row.BookCopyId) || Enumerable.Contains(accessions, row.AccessionNumber.Trim().ToUpper())))
			.Select(row => new { row.BookCopyId, Accession = row.AccessionNumber.Trim().ToUpper() }));
		var blocked = copyRows.Where(copy => open.Any(loan => loan.BookCopyId == copy.Id || loan.Accession == copy.NormalizedAccessionNumber)).Select(copy => copy.Id).ToHashSet();
		var people = await eligibility.ReadAsync(requestRows.Select(row => row.CanonicalMemberId ?? row.MemberId));
		return new(book, requestRows, holdRows, copyRows, people, blocked, await ConfirmedTodayAsync(), await eligibility.GetAcademicYearIdsAsync());
	}

	private sealed record AllocationView(Book Book, System.Collections.Generic.List<BookReservation> Requests,
		System.Collections.Generic.List<LibraryHold> Holds, System.Collections.Generic.List<BookCopy> Copies,
		Dictionary<Guid, LibraryReservationBorrowerFacts> People, HashSet<Guid> LoanCopyIds, DateOnly? Today, HashSet<Guid> YearIds);
}
