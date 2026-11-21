using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockVerificationSession : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const int MaximumNameLength = 160;
	public const int MaximumCancellationReasonLength = 500;

	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public Guid? SectionId { get; private set; }
	public Guid? ShelfId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public DateOnly SchoolDate { get; private set; }
	public int ExpectedCopyCount { get; private set; }
	public int ObservationCount { get; private set; }
	public LibraryStockVerificationStatus Status { get; private set; }
	public Guid OpenedBy { get; private set; }
	public DateTimeOffset OpenedAt { get; private set; }
	public DateTimeOffset? LastObservedAt { get; private set; }
	public Guid? ReviewStartedBy { get; private set; }
	public DateTimeOffset? ReviewStartedAt { get; private set; }
	public Guid? FinalizedBy { get; private set; }
	public DateTimeOffset? FinalizedAt { get; private set; }
	public string? FinalizedEvidenceFingerprint { get; private set; }
	public Guid? CancelledBy { get; private set; }
	public DateTimeOffset? CancelledAt { get; private set; }
	public string? CancellationReason { get; private set; }

	private LibraryStockVerificationSession() { }

	public LibraryStockVerificationSession(Guid id, Guid tenantId, string name, Guid? sectionId, Guid? shelfId,
		DateOnly schoolDate, int expectedCopyCount, Guid openedBy, DateTimeOffset openedAt, Guid? academicYearId = null) : base(id)
	{
		var trimmedName = name?.Trim();
		if (id == Guid.Empty || tenantId == Guid.Empty || sectionId == Guid.Empty || shelfId == Guid.Empty ||
			(shelfId.HasValue || academicYearId.HasValue) && !sectionId.HasValue || academicYearId == Guid.Empty ||
			schoolDate == default || expectedCopyCount < 0 ||
			string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > MaximumNameLength)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
		ValidateOperation(openedBy, openedAt, default);
		TenantId = tenantId;
		Name = trimmedName;
		SectionId = sectionId;
		ShelfId = shelfId;
		AcademicYearId = academicYearId;
		SchoolDate = schoolDate;
		ExpectedCopyCount = expectedCopyCount;
		OpenedBy = openedBy;
		OpenedAt = openedAt.ToUniversalTime();
		Status = LibraryStockVerificationStatus.Counting;
	}

	public void RecordObservation(Guid actorId, DateTimeOffset observedAt)
	{
		RequireState(LibraryStockVerificationStatus.Counting);
		ValidateOperation(actorId, observedAt, LastObservedAt ?? OpenedAt);
		ObservationCount = checked(ObservationCount + 1);
		LastObservedAt = observedAt.ToUniversalTime();
	}

	public void StartReview(Guid actorId, DateTimeOffset reviewedAt)
	{
		RequireState(LibraryStockVerificationStatus.Counting);
		ValidateOperation(actorId, reviewedAt, LastObservedAt ?? OpenedAt);
		ReviewStartedBy = actorId;
		ReviewStartedAt = reviewedAt.ToUniversalTime();
		Status = LibraryStockVerificationStatus.InReview;
	}

	public void FinalizeReview(Guid actorId, DateTimeOffset finalizedAt, string evidenceFingerprint)
	{
		RequireState(LibraryStockVerificationStatus.InReview);
		ValidateOperation(actorId, finalizedAt, ReviewStartedAt ?? OpenedAt);
		if (!LibraryStockVerificationCommand.IsFingerprint(evidenceFingerprint))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
		FinalizedBy = actorId;
		FinalizedAt = finalizedAt.ToUniversalTime();
		FinalizedEvidenceFingerprint = evidenceFingerprint;
		Status = LibraryStockVerificationStatus.Finalized;
	}

	public void Cancel(Guid actorId, DateTimeOffset cancelledAt, string reason)
	{
		if (Status is not (LibraryStockVerificationStatus.Counting or LibraryStockVerificationStatus.InReview))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		ValidateOperation(actorId, cancelledAt, ReviewStartedAt ?? LastObservedAt ?? OpenedAt);
		var trimmedReason = reason?.Trim();
		if (string.IsNullOrWhiteSpace(trimmedReason) || trimmedReason.Length > MaximumCancellationReasonLength)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
		CancelledBy = actorId;
		CancelledAt = cancelledAt.ToUniversalTime();
		CancellationReason = trimmedReason;
		Status = LibraryStockVerificationStatus.Cancelled;
	}

	private void RequireState(LibraryStockVerificationStatus expected)
	{
		if (Status != expected) throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
	}

	private static void ValidateOperation(Guid actorId, DateTimeOffset occurredAt, DateTimeOffset notBefore)
	{
		if (actorId == Guid.Empty || occurredAt == default || occurredAt < notBefore)
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidInput);
	}
}
