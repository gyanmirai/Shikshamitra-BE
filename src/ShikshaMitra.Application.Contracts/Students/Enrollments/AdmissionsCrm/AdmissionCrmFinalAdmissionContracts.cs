using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class UpsertAdmissionCrmSeatPoolDto
{
	public string? ConcurrencyStamp { get; set; }
	public Guid? PlaybookId { get; set; }
	public required string Code { get; set; }
	public required string Name { get; set; }
	public string? RoundCode { get; set; }
	public string? Campus { get; set; }
	public string? DesiredClass { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public string? ApplicantCategory { get; set; }
	public int OperationalTarget { get; set; }
	public int ApprovedCapacity { get; set; }
	public bool IsActive { get; set; } = true;
}

public class AdmissionCrmSeatPoolDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid? PlaybookId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? RoundCode { get; set; }
	public string? Campus { get; set; }
	public string? DesiredClass { get; set; }
	public string? Board { get; set; }
	public string? Medium { get; set; }
	public string? ApplicantCategory { get; set; }
	public int OperationalTarget { get; set; }
	public int ApprovedCapacity { get; set; }
	public int ReservedSeats { get; set; }
	public int AcceptedSeats { get; set; }
	public int ActivatedSeats { get; set; }
	public int WaitlistedApplicants { get; set; }
	public int ReleasedSeats { get; set; }
	public int CancelledSeats { get; set; }
	public int OccupiedSeats { get; set; }
	public int RemainingCapacity { get; set; }
	public bool IsOverCapacity { get; set; }
	public AdmissionCrmSeatPoolStatus Status { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class GetAdmissionCrmSeatPoolListInput : PagedAndSortedResultRequestDto
{
	public string? Filter { get; set; }
	public AdmissionCrmSeatPoolStatus? Status { get; set; }
}

public class CreateAdmissionCrmSeatOfferDto
{
	public Guid SeatPoolId { get; set; }
	public Guid? RegistrationApplicationId { get; set; }
	public Guid? PlaybookId { get; set; }
	public AdmissionCrmSeatOfferType OfferType { get; set; }
	public IReadOnlyDictionary<string, string?> Terms { get; set; } = new Dictionary<string, string?>();
	public string Language { get; set; } = "en";
	public DateTime ValidFrom { get; set; }
	public DateTime ValidUntil { get; set; }
}

public class AdmissionCrmSeatOfferPreviewDto
{
	public Guid LeadId { get; set; }
	public Guid SeatPoolId { get; set; }
	public int RemainingCapacity { get; set; }
	public bool IsReady { get; set; }
	public IReadOnlyList<string> BlockingReasons { get; set; } = [];
}

public class AdmissionCrmSeatOfferDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid SeatPoolId { get; set; }
	public Guid? RegistrationApplicationId { get; set; }
	public Guid? PlaybookId { get; set; }
	public Guid? PreviousOfferId { get; set; }
	public Guid? ReservationId { get; set; }
	public string OfferNumber { get; set; } = string.Empty;
	public int Version { get; set; }
	public AdmissionCrmSeatOfferType OfferType { get; set; }
	public AdmissionCrmSeatOfferStatus Status { get; set; }
	public IReadOnlyDictionary<string, string?> Terms { get; set; } = new Dictionary<string, string?>();
	public string Language { get; set; } = string.Empty;
	public DateTime ValidFrom { get; set; }
	public DateTime ValidUntil { get; set; }
	public Guid? SubmittedByUserId { get; set; }
	public DateTime? SubmittedAt { get; set; }
	public Guid? ApprovedByUserId { get; set; }
	public DateTime? ApprovedAt { get; set; }
	public DateTime? IssuedAt { get; set; }
	public DateTime? OpenedAt { get; set; }
	public DateTime? RespondedAt { get; set; }
	public string? ResponseSource { get; set; }
	public string? ResponseReason { get; set; }
	public IReadOnlyDictionary<string, object?> ResponseSnapshot { get; set; } = new Dictionary<string, object?>();
	public AdmissionCrmSeatReservationDto? Reservation { get; set; }
}

public class AdmissionCrmSeatReservationDto : EntityDto<Guid>
{
	public Guid LeadId { get; set; }
	public Guid SeatPoolId { get; set; }
	public Guid SeatOfferId { get; set; }
	public AdmissionCrmSeatReservationStatus Status { get; set; }
	public DateTime ReservedAt { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime? CommittedAt { get; set; }
	public DateTime? ReleasedAt { get; set; }
	public string? ReleaseReason { get; set; }
	public bool CapacityOverrideUsed { get; set; }
	public string? CapacityOverrideReason { get; set; }
}

public class GetAdmissionCrmSeatOfferListInput : PagedAndSortedResultRequestDto
{
	public Guid? LeadId { get; set; }
	public Guid? SeatPoolId { get; set; }
	public AdmissionCrmSeatOfferStatus? Status { get; set; }
}

public class IssueAdmissionCrmSeatOfferDto
{
	public bool CapacityOverride { get; set; }
	public string? CapacityOverrideReason { get; set; }
}

public class RespondAdmissionCrmSeatOfferDto
{
	public string ResponseSource { get; set; } = "Staff recorded";
	public string? Reason { get; set; }
	public string Language { get; set; } = "en";
	public IReadOnlyDictionary<string, bool> Declarations { get; set; } = new Dictionary<string, bool>();
	public string? EvidenceReference { get; set; }
}

public class AdmissionCrmReasonDto
{
	public required string Reason { get; set; }
}

public class SupersedeAdmissionCrmSeatOfferDto : CreateAdmissionCrmSeatOfferDto
{
	public required string Reason { get; set; }
}

public class AdmissionCrmOfferExpiryRunResultDto
{
	public Guid AcademicYearId { get; set; }
	public int EvaluatedCount { get; set; }
	public int ExpiredCount { get; set; }
	public int FailedCount { get; set; }
	public IReadOnlyList<Guid> FailedOfferIds { get; set; } = [];
}

public class CreateAdmissionCrmWaitlistEntryDto
{
	public Guid SeatPoolId { get; set; }
	public int PriorityScore { get; set; }
}

public class AdmissionCrmWaitlistEntryDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid SeatPoolId { get; set; }
	public int PriorityScore { get; set; }
	public long PositionSequence { get; set; }
	public int CurrentPosition { get; set; }
	public AdmissionCrmWaitlistStatus Status { get; set; }
	public DateTime AddedAt { get; set; }
	public DateTime? PromotedAt { get; set; }
	public Guid? OfferId { get; set; }
	public DateTime? ClosedAt { get; set; }
	public string? ClosureReason { get; set; }
	public bool IsProtected { get; set; }
	public string? ProtectionReason { get; set; }
	public bool PromotionPaused { get; set; }
	public string? PromotionPauseReason { get; set; }
}

public class UpdateAdmissionCrmWaitlistGovernanceDto
{
	public int PriorityScore { get; set; }
	public bool IsProtected { get; set; }
	public bool PromotionPaused { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class AdmissionCrmWaitlistHistoryDto : EntityDto<Guid>
{
	public Guid WaitlistEntryId { get; set; }
	public AdmissionCrmWaitlistHistoryAction Action { get; set; }
	public int? PreviousPriorityScore { get; set; }
	public int PriorityScore { get; set; }
	public string Reason { get; set; } = string.Empty;
	public Guid? RelatedOfferId { get; set; }
	public Guid? ActorUserId { get; set; }
	public DateTime OccurredAt { get; set; }
}

public class RunAdmissionCrmWaitlistPromotionDto
{
	public Guid SeatPoolId { get; set; }
	public int MaximumOffers { get; set; } = 1;
	public CreateAdmissionCrmSeatOfferDto Offer { get; set; } = new();
}

public class AdmissionCrmWaitlistPromotionRunResultDto
{
	public Guid SeatPoolId { get; set; }
	public int EvaluatedCount { get; set; }
	public int PreparedCount { get; set; }
	public IReadOnlyList<Guid> OfferIds { get; set; } = [];
	public IReadOnlyList<Guid> FailedEntryIds { get; set; } = [];
}

public class PrepareAdmissionCrmWaitlistPromotionDto : CreateAdmissionCrmSeatOfferDto
{
	public bool OverridePriority { get; set; }
	public string? OverrideReason { get; set; }
}

public class RecordAdmissionCrmAdmissionDecisionDto
{
	public AdmissionCrmAdmissionDecisionStatus Status { get; set; }
	public Guid? SeatPoolId { get; set; }
	public Guid? OfferId { get; set; }
	public required string Reason { get; set; }
	public bool OverrideUsed { get; set; }
	public string? OverrideReason { get; set; }
}

public class AdmissionCrmAdmissionDecisionDto : EntityDto<Guid>
{
	public Guid AcademicYearId { get; set; }
	public Guid LeadId { get; set; }
	public Guid? SeatPoolId { get; set; }
	public Guid? OfferId { get; set; }
	public AdmissionCrmAdmissionDecisionStatus Status { get; set; }
	public string? Reason { get; set; }
	public bool OverrideUsed { get; set; }
	public string? OverrideReason { get; set; }
	public Guid? DecidedByUserId { get; set; }
	public DateTime? DecidedAt { get; set; }
}
