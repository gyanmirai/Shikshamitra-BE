using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Reservations;

public sealed class LibraryHold : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReservationId { get; private set; }
	public Guid BookId { get; private set; }
	public Guid CopyId { get; private set; }
	public LibraryHoldState State { get; private set; }
	public DateTimeOffset AllocatedAt { get; private set; }
	public DateTimeOffset? EndedAt { get; private set; }
	public DateOnly? SchoolPickupDeadline { get; private set; }
	public LibraryCivilDateProvenance PickupDeadlineProvenance { get; private set; }
	public string? TimeZoneId { get; private set; }
	public string PolicySnapshotJson { get; private set; } = string.Empty;
	public string CalendarSnapshotJson { get; private set; } = string.Empty;
	public string? EndReason { get; private set; }
	public Guid? RelatedLoanId { get; private set; }
	private LibraryHold() { }

	public LibraryHold(Guid id, Guid? tenantId, Guid requestId, Guid bookId, Guid copyId, DateTimeOffset now,
		DateOnly? deadline, LibraryCivilDateProvenance provenance, string? timeZoneId, string policySnapshot, string calendarSnapshot) : base(id)
	{
		if (id == Guid.Empty || requestId == Guid.Empty || bookId == Guid.Empty || copyId == Guid.Empty ||
			string.IsNullOrWhiteSpace(policySnapshot) || string.IsNullOrWhiteSpace(calendarSnapshot))
			throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		TenantId = tenantId; ReservationId = requestId; BookId = bookId; CopyId = copyId; AllocatedAt = now.ToUniversalTime();
		SchoolPickupDeadline = deadline; PickupDeadlineProvenance = provenance; TimeZoneId = timeZoneId;
		PolicySnapshotJson = policySnapshot; CalendarSnapshotJson = calendarSnapshot;
	}

	public void Finish(LibraryHoldState state, string reason, DateTimeOffset now, Guid? loanId = null)
	{
		if (State != LibraryHoldState.Allocated || state == LibraryHoldState.Allocated || !Enum.IsDefined(state) ||
			state == LibraryHoldState.Collected && (!loanId.HasValue || loanId == Guid.Empty))
			throw new BusinessException(LibraryReservationErrorCodes.InvalidTransition);
		State = state; EndReason = Books.BookReservation.Reason(reason); EndedAt = now.ToUniversalTime(); RelatedLoanId = loanId;
	}
}
