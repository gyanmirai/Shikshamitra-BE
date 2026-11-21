using System;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class BookReservation : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime? RequestedDate { get; private set; }
	public DateTime? ReserveDueDate { get; private set; }
	public Guid? CanonicalMemberId { get; private set; }
	public Guid? OriginalMemberId { get; private set; }
	public int? MemberRole { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public Guid? BorrowerBoardId { get; private set; }
	public Guid? BorrowerMediumId { get; private set; }
	public Guid? BorrowerStageId { get; private set; }
	public Guid? BorrowerStandardId { get; private set; }
	public Guid? BorrowerDivisionId { get; private set; }
	public string? BorrowerScopeSnapshotJson { get; private set; }
	public LibraryBorrowerScopeProvenance BorrowerScopeProvenance { get; private set; }
	public Guid? BorrowerScopeCaptureId { get; private set; }
	public int? BorrowerScopeCaptureVersion { get; private set; }
	public long? QueueSequence { get; private set; }
	public Guid? PickupSectionId { get; private set; }
	public Guid? PickupCampusId { get; private set; }
	public string? PickupLabel { get; private set; }
	public LibraryReservationQueueProvenance QueueProvenance { get; private set; }
	public LibraryReservationState State { get; private set; } = LibraryReservationState.NeedsReview;
	public DateOnly? SchoolRequestedDate { get; private set; }
	public DateOnly? SchoolRequestExpiryDate { get; private set; }
	public LibraryCivilDateProvenance RequestedDateProvenance { get; private set; }
	public LibraryCivilDateProvenance RequestExpiryDateProvenance { get; private set; }
	public string? PolicySnapshotJson { get; private set; }
	public string? ReviewReason { get; private set; }
	public string? TerminalReason { get; private set; }
	public DateTimeOffset? EndedAt { get; private set; }
	public string Revision { get; private set; } = Guid.NewGuid().ToString("N");

	protected BookReservation() { }

	public BookReservation(Guid id, Guid bookId, Guid memberId, DateTime requestedDate, DateTime reserveDueDate, Guid? tenantId = null) : base(id)
	{
		TenantId = tenantId; BookId = bookId; MemberId = memberId; OriginalMemberId = memberId;
		RequestedDate = requestedDate; ReserveDueDate = reserveDueDate;
		ReviewReason = "Review the legacy member, school dates, academic context, and queue order before allocation.";
	}

	public static BookReservation Create(Guid id, Guid? tenantId, Guid bookId, Guid personId, Guid originalMemberId,
		int role, Guid yearId, long sequence, DateOnly requestedDate, DateOnly? expiryDate, string policySnapshot,
		DateTime? rawRequestedDate = null, DateTime? rawExpiryDate = null, LibraryReservationBorrowerContext? borrowerContext = null,
		LibraryReservationPickup? pickup = null)
	{
		var result = new BookReservation { Id = id, TenantId = tenantId, BookId = bookId, MemberId = personId,
			OriginalMemberId = originalMemberId, RequestedDate = rawRequestedDate, ReserveDueDate = rawExpiryDate,
			PickupSectionId = pickup?.SectionId, PickupCampusId = pickup?.CampusId, PickupLabel = pickup?.Label };
		result.SetAdmission(personId, role, yearId, sequence, requestedDate, expiryDate, policySnapshot,
			LibraryCivilDateProvenance.ExplicitSchoolDate, LibraryReservationQueueProvenance.ServerSequence, borrowerContext);
		return result;
	}

	public void Review(Guid personId, int role, Guid yearId, long sequence, DateOnly date, DateOnly? expiry, string policySnapshot,
		LibraryReservationBorrowerContext? borrowerContext = null)
	{
		if (State != LibraryReservationState.NeedsReview) throw new BusinessException(LibraryReservationErrorCodes.InvalidTransition);
		SetAdmission(personId, role, yearId, sequence, date, expiry, policySnapshot,
			LibraryCivilDateProvenance.ReviewedLegacy, LibraryReservationQueueProvenance.ReviewedLegacy, borrowerContext);
	}

	public void Ready()
	{
		if (State != LibraryReservationState.Waiting) throw new BusinessException(LibraryReservationErrorCodes.InvalidTransition);
		State = LibraryReservationState.Ready; Touch();
	}

	public void RequireReview(string reason)
	{
		EnsureActive(); State = LibraryReservationState.NeedsReview; ReviewReason = Reason(reason); Touch();
	}

	public void Finish(LibraryReservationState state, string reason, DateTimeOffset now)
	{
		EnsureActive();
		if (state is not (LibraryReservationState.Cancelled or LibraryReservationState.Expired or LibraryReservationState.Fulfilled) ||
			state == LibraryReservationState.Fulfilled && State != LibraryReservationState.Ready)
			throw new BusinessException(LibraryReservationErrorCodes.InvalidTransition);
		State = state; TerminalReason = Reason(reason); EndedAt = now.ToUniversalTime(); Touch();
	}

	public void EnsureRevision(string? revision)
	{
		if (string.IsNullOrWhiteSpace(revision) || revision != Revision)
			throw new BusinessException(LibraryReservationErrorCodes.Conflict, "This reservation changed. Reload it before saving.");
	}

	public void BindStaffScopeCapture(LibraryBorrowerScopeCapture capture)
	{
		var expected = QueueProvenance == LibraryReservationQueueProvenance.ReviewedLegacy
			? LibraryBorrowerScopeProvenance.ReviewedLegacy : LibraryBorrowerScopeProvenance.CapturedAtOperation;
		if (MemberRole != 1 || capture.Provenance != expected) throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		capture.EnsureSource(TenantId, LibraryBorrowerScopeSourceKind.Reservation, Id, CanonicalMemberId ?? MemberId, AcademicYearId);
		if (BorrowerScopeCaptureVersion.HasValue && capture.Version <= BorrowerScopeCaptureVersion.Value)
			throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		BorrowerScopeCaptureId = capture.Id; BorrowerScopeCaptureVersion = capture.Version; BorrowerScopeProvenance = capture.Provenance;
	}

	private void SetAdmission(Guid personId, int role, Guid yearId, long sequence, DateOnly date, DateOnly? expiry,
		string policySnapshot, LibraryCivilDateProvenance provenance, LibraryReservationQueueProvenance queueProvenance,
		LibraryReservationBorrowerContext? borrowerContext)
	{
		if (Id == Guid.Empty || BookId == Guid.Empty || personId == Guid.Empty || yearId == Guid.Empty || role is < 0 or > 1 ||
			sequence <= 0 || date == default || expiry.HasValue && expiry < date || string.IsNullOrWhiteSpace(policySnapshot))
			throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		CanonicalMemberId = personId; MemberRole = role; AcademicYearId = yearId; QueueSequence = sequence;
		if (role == 0) { BorrowerScopeCaptureId = null; BorrowerScopeCaptureVersion = null; }
		BorrowerBoardId = borrowerContext?.BoardId; BorrowerMediumId = borrowerContext?.MediumId;
		BorrowerStageId = borrowerContext?.StageId; BorrowerStandardId = borrowerContext?.StandardId;
		BorrowerDivisionId = borrowerContext?.DivisionId; BorrowerScopeSnapshotJson = borrowerContext?.ScopeSnapshotJson;
		BorrowerScopeProvenance = borrowerContext == null ? LibraryBorrowerScopeProvenance.Unknown :
			queueProvenance == LibraryReservationQueueProvenance.ReviewedLegacy
				? LibraryBorrowerScopeProvenance.ReviewedLegacy : LibraryBorrowerScopeProvenance.CapturedAtOperation;
		SchoolRequestedDate = date; SchoolRequestExpiryDate = expiry;
		RequestedDateProvenance = provenance; RequestExpiryDateProvenance = provenance;
		QueueProvenance = queueProvenance; PolicySnapshotJson = policySnapshot; State = LibraryReservationState.Waiting;
		ReviewReason = null; Touch();
	}

	private void EnsureActive()
	{
		if (State is LibraryReservationState.Cancelled or LibraryReservationState.Expired or LibraryReservationState.Fulfilled)
			throw new BusinessException(LibraryReservationErrorCodes.InvalidTransition);
	}
	private void Touch() => Revision = Guid.NewGuid().ToString("N");
	internal static string Reason(string reason) => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500
		? reason.Trim() : throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest, "A reason is required.");
}
