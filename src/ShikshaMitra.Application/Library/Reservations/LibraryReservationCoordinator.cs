using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Reservations;

public sealed record LibraryIssuePriorityResult(bool CanIssue, string? BlockedReason, string Revision);

[RemoteService(false)]
public partial class LibraryReservationCoordinator(IRepository<BookReservation, Guid> requests,
	IRepository<LibraryHold, Guid> holds, IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	IRepository<BookIssue, Guid> loans, IRepository<LibraryCalendarClosure, Guid> closures,
	LibraryReservationEligibilityReader eligibility, LibraryBorrowingPolicyResolver policies, LibraryCalendarService calendar,
	LibraryReservationManager history, LibraryCopyEventRecorder copyEvents, ILibraryReservationNotificationSink notificationSink,
	ILibraryInventoryLock inventoryLock, IClock clock, LibraryStaffScopeCaptureService staffCaptures,
	LibraryReservationPickupReader pickupLocations)
	: ApplicationService, ITransientDependency
{
	private static bool Active(LibraryReservationState state) => state is LibraryReservationState.Waiting or LibraryReservationState.Ready or LibraryReservationState.NeedsReview;
	private DateTimeOffset Now => new(clock.Now.ToUniversalTime());
	private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { PropertyNameCaseInsensitive = true };
	private static bool IsManual(LibraryBorrowingPolicySnapshotDto snapshot) => snapshot.IsLegacyManualDate || snapshot.Terms.DueDateMode == 0;

	public async Task RebalanceTenantAsync()
	{
		var requestedBooks = await AsyncExecuter.ToListAsync((await requests.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview))
			.Select(row => row.BookId).Distinct());
		var heldBooks = await AsyncExecuter.ToListAsync((await holds.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.State == LibraryHoldState.Allocated)
			.Select(row => row.BookId).Distinct());
		foreach (var bookId in requestedBooks.Concat(heldBooks).Distinct().OrderBy(id => id)) await RebalanceAsync(bookId);
	}

	public async Task<BookReservation> CreateAsync(Guid bookId, Guid memberId, Guid yearId, DateOnly requestedDate,
		DateOnly? expiryDate, LibraryOperationActor actor, DateTime? rawRequestedDate = null, DateTime? rawExpiryDate = null,
		Guid? pickupSectionId = null)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var book = await books.GetAsync(bookId);
		var person = await eligibility.RequireAsync(memberId, yearId);
		var pickup = await ReadPickupAsync(pickupSectionId);
		if (pickupSectionId.HasValue && pickup == null) throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest,
			"Choose an active Library pickup location in this school.");
		var snapshot = await ResolvePolicyAsync(book, pickup?.SectionId, person, yearId, requestedDate);
		if (IsManual(snapshot) && !expiryDate.HasValue)
			throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest, "Enter an explicit reservation expiry date while this school uses manual borrowing rules.");
		var aliases = person.MemberIds.ToArray();
		var current = await AsyncExecuter.ToListAsync((await requests.GetQueryableAsync()).Where(row =>
			row.TenantId == CurrentTenant.Id && (row.CanonicalMemberId == person.PersonId || !row.CanonicalMemberId.HasValue && Enumerable.Contains(aliases, row.MemberId)) &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview)));
		if (current.Any(row => row.BookId == bookId)) throw new BusinessException(LibraryErrorCodes.DuplicateReservation);
		if (current.Count >= snapshot.Terms.ReservationLimit) throw new BusinessException(LibraryErrorCodes.ReservationLimitExceeded);
		var request = BookReservation.Create(GuidGenerator.Create(), CurrentTenant.Id, bookId, person.PersonId, memberId,
			person.Role, yearId, await NextSequenceAsync(), requestedDate, expiryDate, JsonSerializer.Serialize(snapshot), rawRequestedDate, rawExpiryDate,
			CaptureStudentScope(person, yearId, LibraryBorrowerScopeProvenance.CapturedAtOperation), pickup);
		if (person.Role == 1) request.BindStaffScopeCapture(await staffCaptures.CaptureAsync(LibraryBorrowerScopeSourceKind.Reservation,
			request.Id, person.PersonId, yearId, LibraryBorrowerScopeProvenance.CapturedAtOperation, actor));
		await requests.InsertAsync(request, true);
		await history.RecordAsync(request, LibraryReservationState.Waiting, "{}", actor, "Reservation requested.", "Requested");
		await RebalanceAsync(bookId, requestedDate);
		return request;
	}

	public async Task CancelAsync(BookReservation request, string reason, string revision, LibraryOperationActor actor)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		request = await requests.GetAsync(request.Id);
		request.EnsureRevision(revision);
		await EndAsync(request, LibraryReservationState.Cancelled, LibraryHoldState.Released, reason, actor);
		if (request.State != LibraryReservationState.Cancelled)
			throw new BusinessException(LibraryReservationErrorCodes.ReviewRequired, request.ReviewReason);
		await RebalanceAsync(request.BookId);
	}

	public async Task ReviewAsync(BookReservation request, Guid memberId, Guid yearId, DateOnly date, DateOnly? expiry,
		string reason, string revision, LibraryOperationActor actor)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		request = await requests.GetAsync(request.Id);
		request.EnsureRevision(revision);
		var facts = await eligibility.RequireAsync(memberId, yearId);
		var aliases = facts.MemberIds.ToArray();
		var otherRequests = (await requests.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.Id != request.Id &&
			(row.CanonicalMemberId == facts.PersonId || !row.CanonicalMemberId.HasValue && Enumerable.Contains(aliases, row.MemberId)) &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview));
		if (await AsyncExecuter.AnyAsync(otherRequests.Where(row => row.BookId == request.BookId)))
			throw new BusinessException(LibraryErrorCodes.DuplicateReservation);
		var policy = await ResolvePolicyAsync(await books.GetAsync(request.BookId), request.PickupSectionId, facts, yearId, date);
		if (await AsyncExecuter.CountAsync(otherRequests) >= policy.Terms.ReservationLimit)
			throw new BusinessException(LibraryErrorCodes.ReservationLimitExceeded);
		if (IsManual(policy) && !expiry.HasValue) throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		var before = LibraryReservationManager.Capture(request);
		var previous = request.State;
		var existingHold = await holds.FindAsync(row => row.ReservationId == request.Id && row.State == LibraryHoldState.Allocated);
		if (existingHold != null)
		{
			var copy = await copies.GetAsync(existingHold.CopyId);
			if (copy.BookId != request.BookId || await HasOpenLoanAsync(copy))
				throw new BusinessException(LibraryReservationErrorCodes.ReviewRequired, "Resolve the conflicting physical copy or loan before reviewing this hold.");
			existingHold.Finish(LibraryHoldState.Released, reason, Now);
			await holds.UpdateAsync(existingHold, true);
			if (copy.Status == BookCopyStatus.Reserved)
			{
				var copyBefore = LibraryCopySnapshot.Capture(copy);
				copy.Status = BookCopyStatus.Available;
				await copyEvents.RecordAsync(copyBefore, copy, LibraryCopyEventType.HoldReleased, actor, reason);
				await copies.UpdateAsync(copy, true);
			}
			await history.RecordAsync(request, previous, before, actor, reason, "HoldReleasedForReview", existingHold);
		}
		request.Review(facts.PersonId, facts.Role, yearId, await NextSequenceAsync(), date, expiry, JsonSerializer.Serialize(policy),
			CaptureStudentScope(facts, yearId, LibraryBorrowerScopeProvenance.ReviewedLegacy));
		if (facts.Role == 1) request.BindStaffScopeCapture(await staffCaptures.CaptureAsync(LibraryBorrowerScopeSourceKind.Reservation,
			request.Id, facts.PersonId, yearId, LibraryBorrowerScopeProvenance.ReviewedLegacy, actor));
		await requests.UpdateAsync(request, true);
		await history.RecordAsync(request, previous, before, actor, reason, "Reviewed");
		await RebalanceAsync(request.BookId, date);
	}

	public async Task<LibraryHold?> PrepareIssueAsync(BookCopy copy, Guid canonicalMemberId, DateOnly schoolIssueDate)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		await RebalanceAsync(copy.BookId, schoolIssueDate);
		var priority = await ReadIssuePriorityAsync(copy, canonicalMemberId);
		if (!priority.CanIssue) throw new BusinessException(LibraryReservationErrorCodes.PriorityBlocked, priority.BlockedReason);
		var hold = await holds.FindAsync(row => row.TenantId == CurrentTenant.Id && row.CopyId == copy.Id && row.State == LibraryHoldState.Allocated);
		if (hold == null && await requests.AnyAsync(row => row.TenantId == CurrentTenant.Id && row.BookId == copy.BookId &&
			(!row.PickupSectionId.HasValue || row.PickupSectionId == copy.SectionId) &&
			(row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.NeedsReview)))
			throw new BusinessException(LibraryReservationErrorCodes.PriorityBlocked, "Complete reservation allocation or review before issuing this copy.");
		return hold;
	}

	public async Task CompleteIssueAsync(BookCopy copy, Guid canonicalMemberId, Guid loanId, LibraryOperationActor actor)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var hold = await holds.FindAsync(row => row.TenantId == CurrentTenant.Id && row.CopyId == copy.Id && row.State == LibraryHoldState.Allocated);
		if (hold == null) return;
		var request = await requests.GetAsync(hold.ReservationId);
		var loan = await loans.GetAsync(loanId);
		if (request.CanonicalMemberId != canonicalMemberId || loan.BookCopyId != copy.Id || loan.MemberId != canonicalMemberId || loan.TenantId != CurrentTenant.Id)
			throw new BusinessException(LibraryReservationErrorCodes.HoldNotFound);
		var before = LibraryReservationManager.Capture(request);
		var previous = request.State;
		hold.Finish(LibraryHoldState.Collected, "Collected through circulation.", Now, loanId);
		request.Finish(LibraryReservationState.Fulfilled, "Collected through circulation.", Now);
		await holds.UpdateAsync(hold, true);
		await requests.UpdateAsync(request, true);
		await history.RecordAsync(request, previous, before, actor, "Collected through circulation.", "Collected", hold, loanId);
	}

	private async Task<long> NextSequenceAsync()
	{
		var last = await AsyncExecuter.FirstOrDefaultAsync((await requests.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && row.QueueSequence.HasValue)
			.OrderByDescending(row => row.QueueSequence).Select(row => row.QueueSequence!.Value));
		return checked(last + 1);
	}

	private async Task<DateOnly?> ConfirmedTodayAsync()
	{
		var settings = await calendar.GetSettingsAsync();
		return settings.Source == "Library" && !settings.NeedsConfiguration ? await calendar.TodayAsync() : null;
	}

	private async Task<LibraryReservationPickup?> ReadPickupAsync(Guid? sectionId) =>
		sectionId is Guid section && CurrentTenant.Id is Guid tenant ? await pickupLocations.FindAsync(tenant, section) : null;

	private async Task<LibraryBorrowingPolicySnapshotDto> ResolvePolicyAsync(Book book, Guid? sectionId,
		LibraryReservationBorrowerFacts facts, Guid yearId, DateOnly asOf)
	{
		var decision = await policies.ResolveAsync(new(yearId, facts.Role, asOf, book.CategoryId, sectionId, facts.StageId, facts.StandardId));
		var terms = decision.Policy?.Terms;
		var automatic = terms?.DueDateMode == LibraryDueDateMode.Automatic;
		var zone = await calendar.GetSettingsAsync();
		return new()
		{
			PolicyVersionId = decision.Policy?.Id, PolicyId = decision.Policy?.PolicyId, Version = decision.Policy?.Version,
			IsLegacyManualDate = decision.IsLegacyManualDate, Role = facts.Role, EffectiveFrom = decision.Policy?.EffectiveFrom,
			EffectiveTo = decision.Policy?.EffectiveTo, TimeZoneId = zone.TimeZoneId, TimeZoneSource = zone.Source,
			NeedsCalendarConfiguration = zone.NeedsConfiguration,
			Terms = new() { DueDateMode = (int?)terms?.DueDateMode, MaxBooksAtTime = decision.MaxBooksAtTime, ReservationLimit = decision.ReservationLimit,
				LoanDays = automatic ? terms?.LoanDays : null, DayCountingMode = automatic ? (int?)terms?.DayCountingMode : null, MaxRenewals = terms?.MaxRenewals,
				RenewalDays = automatic ? terms?.RenewalDays : null, PickupWindowDays = automatic ? terms?.PickupWindowDays : null,
				MoveDueDateToNextOpenDay = automatic ? terms?.MoveDueDateToNextOpenDay : null }
		};
	}
}
