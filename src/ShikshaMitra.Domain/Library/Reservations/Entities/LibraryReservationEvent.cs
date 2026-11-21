using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Reservations;

public sealed class LibraryReservationEvent : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ReservationId { get; private set; }
	public long Sequence { get; private set; }
	public string EventType { get; private set; } = string.Empty;
	public Guid? HoldId { get; private set; }
	public Guid? CopyId { get; private set; }
	public Guid? LoanId { get; private set; }
	public Guid? BeforeBorrowerScopeCaptureId { get; private set; }
	public Guid? AfterBorrowerScopeCaptureId { get; private set; }
	public LibraryReservationState PreviousState { get; private set; }
	public LibraryReservationState NextState { get; private set; }
	public LibraryActorKind ActorKind { get; private set; }
	public Guid? ActorId { get; private set; }
	public LibrarySystemPurpose? SystemPurpose { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public DateTimeOffset OccurredAt { get; private set; }
	public string BeforeSnapshotJson { get; private set; } = string.Empty;
	public string AfterSnapshotJson { get; private set; } = string.Empty;
	private LibraryReservationEvent() { }

	public LibraryReservationEvent(Guid id, Guid? tenantId, Guid requestId, long sequence, string eventType,
		LibraryReservationState previous, LibraryReservationState next, LibraryOperationActor actor, string reason,
		DateTimeOffset now, string beforeSnapshot, string afterSnapshot, Guid? holdId = null, Guid? copyId = null, Guid? loanId = null,
		Guid? beforeBorrowerScopeCaptureId = null, Guid? afterBorrowerScopeCaptureId = null) : base(id)
	{
		if (id == Guid.Empty || requestId == Guid.Empty || sequence <= 0 || string.IsNullOrWhiteSpace(eventType) || eventType.Length > 60 ||
			actor == null || !Enum.IsDefined(previous) || !Enum.IsDefined(next)) throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		TenantId = tenantId; ReservationId = requestId; Sequence = sequence; EventType = eventType;
		PreviousState = previous; NextState = next; ActorKind = actor.Kind; ActorId = actor.UserId; SystemPurpose = actor.SystemPurpose;
		Reason = Books.BookReservation.Reason(reason); OccurredAt = now.ToUniversalTime(); HoldId = holdId; CopyId = copyId; LoanId = loanId;
		BeforeSnapshotJson = beforeSnapshot; AfterSnapshotJson = afterSnapshot;
		BeforeBorrowerScopeCaptureId = beforeBorrowerScopeCaptureId; AfterBorrowerScopeCaptureId = afterBorrowerScopeCaptureId;
	}
}
