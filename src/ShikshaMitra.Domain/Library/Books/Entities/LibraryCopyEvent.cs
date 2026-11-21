using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public sealed class LibraryCopyEvent : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid BookId { get; private set; }
	public Guid CopyId { get; private set; }
	public string AccessionNumber { get; private set; } = string.Empty;
	public long Sequence { get; private set; }
	public LibraryCopyEventType EventType { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }
	public LibraryActorKind ActorKind { get; private set; } = LibraryActorKind.User;
	public Guid? ActorId { get; private set; }
	public LibrarySystemPurpose? SystemPurpose { get; private set; }
	public string Reason { get; private set; } = string.Empty;
	public BookCopyStatus PreviousStatus { get; private set; }
	public BookCopyStatus NextStatus { get; private set; }
	public string? PreviousRemarks { get; private set; }
	public string? NextRemarks { get; private set; }
	public Guid FromSectionId { get; private set; }
	public Guid FromShelfId { get; private set; }
	public Guid ToSectionId { get; private set; }
	public Guid ToShelfId { get; private set; }
	public Guid? RelatedLoanId { get; private set; }

	private LibraryCopyEvent() { }

	public LibraryCopyEvent(Guid id, long sequence, LibraryCopyEventType eventType, LibraryCopySnapshot before,
		LibraryCopySnapshot after, Guid actorId, string reason, DateTimeOffset occurredAt, Guid? relatedLoanId)
		: this(id, sequence, eventType, before, after, LibraryOperationActor.User(actorId), reason, occurredAt, relatedLoanId) { }

	public LibraryCopyEvent(Guid id, long sequence, LibraryCopyEventType eventType, LibraryCopySnapshot before,
		LibraryCopySnapshot after, LibraryOperationActor actor, string reason, DateTimeOffset occurredAt, Guid? relatedLoanId) : base(id)
	{
		if (before.CopyId != after.CopyId || before.BookId != after.BookId || before.TenantId != after.TenantId ||
			before.AccessionNumber != after.AccessionNumber || sequence <= 0 || actor == null || !Enum.IsDefined(eventType))
			throw new BusinessException(LibraryErrorCodes.CopyEventInvalid, "The copy event does not match its inventory record.");
		TenantId = after.TenantId;
		BookId = after.BookId;
		CopyId = after.CopyId;
		AccessionNumber = after.AccessionNumber;
		Sequence = sequence;
		EventType = eventType;
		OccurredAt = occurredAt.ToUniversalTime();
		ActorKind = actor.Kind; ActorId = actor.UserId; SystemPurpose = actor.SystemPurpose;
		Reason = LibraryCopyConditionRules.RequireReason(reason);
		PreviousStatus = before.Status;
		NextStatus = after.Status;
		PreviousRemarks = before.Remarks;
		NextRemarks = after.Remarks;
		FromSectionId = before.SectionId;
		FromShelfId = before.ShelfId;
		ToSectionId = after.SectionId;
		ToShelfId = after.ShelfId;
		RelatedLoanId = relatedLoanId;
	}
}
