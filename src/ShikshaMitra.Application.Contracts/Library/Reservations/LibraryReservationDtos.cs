using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Library.Policies;

namespace ShikshaMitra.Library.Reservations;

public class LibraryReservationPreviewDto
{
	public Guid? CanonicalMemberId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public LibraryBorrowingPolicySnapshotDto? Policy { get; set; }
	public bool RequiresExplicitExpiry { get; set; }
	public bool CanCreate { get; set; }
	public List<string> BlockedReasons { get; set; } = [];
}

public class LibraryReservationReviewPreviewRequest
{
	public Guid AcademicYearId { get; set; }
	public Guid MemberId { get; set; }
	public DateOnly RequestedDate { get; set; }
	public DateOnly? RequestExpiryDate { get; set; }
	[Required, StringLength(128)] public string Revision { get; set; } = string.Empty;
}

public class LibraryReservationReviewPreviewDto
{
	public Guid ReservationId { get; set; }
	public Guid? CanonicalMemberId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public LibraryBorrowingPolicySnapshotDto? Policy { get; set; }
	public bool RequiresExplicitExpiry { get; set; }
	public bool CanReview { get; set; }
	public List<string> BlockedReasons { get; set; } = [];
}

public class LibraryReservationCreateRequest
{
	public Guid BookId { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid MemberId { get; set; }
	public DateOnly RequestedDate { get; set; }
	public DateOnly? RequestExpiryDate { get; set; }
	public Guid? PickupSectionId { get; set; }
}

public sealed record LibraryReservationPickupDto(Guid SectionId, Guid CampusId, string Label);

public class LibraryReservationActionRequest
{
	public Guid AcademicYearId { get; set; }
	[Required, StringLength(500, MinimumLength = 1)] public string Reason { get; set; } = string.Empty;
	[Required, StringLength(128)] public string Revision { get; set; } = string.Empty;
}

public class LibraryReservationReviewRequest : LibraryReservationActionRequest
{
	public Guid MemberId { get; set; }
	public DateOnly RequestedDate { get; set; }
	public DateOnly? RequestExpiryDate { get; set; }
}

public class LibraryReservationDto
{
	public Guid Id { get; set; }
	public Guid BookId { get; set; }
	public Guid? PickupSectionId { get; set; }
	public Guid? PickupCampusId { get; set; }
	public string? PickupLabel { get; set; }
	public Guid MemberId { get; set; }
	public string? MemberName { get; set; }
	public string? MemberIdentifier { get; set; }
	public string? ClassDivision { get; set; }
	public int? Role { get; set; }
	public Guid? AcademicYearId { get; set; }
	public int State { get; set; }
	public Guid? BorrowerScopeCaptureId { get; set; }
	public int? BorrowerScopeCaptureVersion { get; set; }
	public int BorrowerScopeProvenance { get; set; }
	public DateTime? LegacyRequestedDate { get; set; }
	public DateTime? LegacyReserveDueDate { get; set; }
	public DateOnly? RequestedDate { get; set; }
	public DateOnly? RequestExpiryDate { get; set; }
	public int RequestedDateProvenance { get; set; }
	public int RequestExpiryDateProvenance { get; set; }
	public bool RequiresReview { get; set; }
	public string? ReviewReason { get; set; }
	public string Revision { get; set; } = string.Empty;
	public bool CanCancel { get; set; }
	public bool CanReview { get; set; }
	public LibraryHoldDto? Hold { get; set; }
	public LibraryBorrowingPolicySnapshotDto? Policy { get; set; }
}

public class LibraryHoldDto
{
	public Guid Id { get; set; }
	public Guid CopyId { get; set; }
	public string AccessionNumber { get; set; } = string.Empty;
	public int State { get; set; }
	public DateTimeOffset AllocatedAt { get; set; }
	public DateOnly? PickupDeadline { get; set; }
	public int PickupDeadlineProvenance { get; set; }
	public string? TimeZoneId { get; set; }
	public Guid? RelatedLoanId { get; set; }
}

public class LibraryReservationEventDto
{
	public Guid Id { get; set; }
	public long Sequence { get; set; }
	public string EventType { get; set; } = string.Empty;
	public Guid? HoldId { get; set; }
	public Guid? CopyId { get; set; }
	public Guid? LoanId { get; set; }
	public Guid? BeforeBorrowerScopeCaptureId { get; set; }
	public Guid? AfterBorrowerScopeCaptureId { get; set; }
	public int PreviousState { get; set; }
	public int NextState { get; set; }
	public int ActorKind { get; set; }
	public Guid? ActorId { get; set; }
	public int? SystemPurpose { get; set; }
	public string Reason { get; set; } = string.Empty;
	public DateTimeOffset OccurredAt { get; set; }
}
