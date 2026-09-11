using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public class StudentOnboardingListInput : PagedAndSortedResultRequestDto
{
	public string? Search { get; set; }
	public StudentOnboardingStatus? Status { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? AssignedToUserId { get; set; }
	public bool? OverdueOnly { get; set; }
	public bool? UnassignedOnly { get; set; }
}

public class StudentOnboardingFilterOptionDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
}

public class StudentOnboardingFilterOptionsDto
{
	public IReadOnlyList<StudentOnboardingFilterOptionDto> AcademicYears { get; set; } = [];
	public IReadOnlyList<StudentOnboardingFilterOptionDto> Standards { get; set; } = [];
}

public class StudentOnboardingAnalyticsDto
{
	public int ActiveCases { get; set; }
	public int Pending { get; set; }
	public int InProgress { get; set; }
	public int ReadyForActivation { get; set; }
	public int NeedsAttention { get; set; }
	public int Overdue { get; set; }
	public int Completed { get; set; }
	public int Cancelled { get; set; }
}

public class StudentOnboardingStepDto
{
	public Guid Id { get; set; }
	public string Key { get; set; } = string.Empty;
	public string Label { get; set; } = string.Empty;
	public int DisplayOrder { get; set; }
	public bool Required { get; set; }
	public StudentOnboardingStepStatus Status { get; set; }
	public string? Detail { get; set; }
	public Guid? CompletedByUserId { get; set; }
	public DateTime? CompletedAt { get; set; }
	public string? WaiverReason { get; set; }
}

public class StudentOnboardingEventDto
{
	public Guid Id { get; set; }
	public string EventType { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? MetadataJson { get; set; }
	public Guid? ActorUserId { get; set; }
	public DateTime OccurredAt { get; set; }
}

public class StudentOnboardingServiceHandoffDto : EntityDto<Guid>
{
	public Guid OnboardingCaseId { get; set; }
	public string ServiceKey { get; set; } = string.Empty;
	public bool Required { get; set; }
	public StudentOnboardingServiceHandoffStatus Status { get; set; }
	public int AttemptCount { get; set; }
	public DateTime? LastAttemptAt { get; set; }
	public DateTime? AcknowledgedAt { get; set; }
	public string? LastError { get; set; }
}

public class StudentOnboardingCaseDto : FullAuditedEntityDto<Guid>
{
	public bool DocumentUploadsEnabled { get; set; } = true;
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid ApplicationId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public Guid ApplicantId { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = string.Empty;
	public Guid OfferingId { get; set; }
	public string OfferingCode { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public string AcademicYearName { get; set; } = string.Empty;
	public Guid IntendedBoardId { get; set; }
	public Guid IntendedMediumId { get; set; }
	public Guid IntendedStageId { get; set; }
	public Guid IntendedStandardId { get; set; }
	public string IntendedStandardName { get; set; } = string.Empty;
	public StudentOnboardingStatus Status { get; set; }
	public Guid? AssignedToUserId { get; set; }
	public string? AssignedToUserName { get; set; }
	public DateTime DueAt { get; set; }
	public bool IsHistoricalBackfill { get; set; }
	public Guid? DivisionId { get; set; }
	public string? DivisionName { get; set; }
	public Guid? SeatReservationId { get; set; }
	public string? GrNumber { get; set; }
	public int? RollNumber { get; set; }
	public Guid? StudentEnrollmentId { get; set; }
	public DateTime? StartedAt { get; set; }
	public DateTime? ReadyAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public DateTime? CancelledAt { get; set; }
	public string? CancellationReason { get; set; }
	public string? LastError { get; set; }
	public string? PrimaryBlocker { get; set; }
	public int ProgressPercent { get; set; }
	public bool IsOverdue { get; set; }
	public IReadOnlyList<StudentOnboardingStepDto> Steps { get; set; } = [];
	public IReadOnlyList<StudentOnboardingEventDto> Events { get; set; } = [];
}

public class StudentOnboardingPlacementOptionDto : EntityDto<Guid>
{
	public string Name { get; set; } = string.Empty;
	public int Capacity { get; set; }
	public int ActiveEnrollments { get; set; }
	public int LiveReservations { get; set; }
	public int AvailableSeats { get; set; }
	public bool IsFull => AvailableSeats <= 0;
}

public class UpdateStudentOnboardingStepDto
{
	public string? ConcurrencyStamp { get; set; }
	public StudentOnboardingStepStatus Status { get; set; }
	public string? Detail { get; set; }
	public string? WaiverReason { get; set; }
}

public class AssignStudentOnboardingOwnerDto
{
	public string? ConcurrencyStamp { get; set; }
	public Guid? UserId { get; set; }
}

public class SetStudentOnboardingPlacementDto
{
	public string? ConcurrencyStamp { get; set; }
	public Guid DivisionId { get; set; }
}

public class AllocateStudentOnboardingIdentifierDto
{
	public string? ConcurrencyStamp { get; set; }
	public Guid NumberSeriesId { get; set; }
	public int RollNumber { get; set; }
	public string? ManualValue { get; set; }
	public long? ManualSequence { get; set; }
	public string? OverrideReason { get; set; }
}

public class ActivateStudentOnboardingDto
{
	public string? ConcurrencyStamp { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
}

public class CancelStudentOnboardingDto
{
	public string? ConcurrencyStamp { get; set; }
	public string Reason { get; set; } = string.Empty;
}

public class UpsertStudentOnboardingPolicyDto
{
	public string? ConcurrencyStamp { get; set; }
	public Guid AcademicYearId { get; set; }
	public Guid? OfferingId { get; set; }
	public Guid? StandardId { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool RequireDocuments { get; set; } = true;
	public bool RequireFamilyDetails { get; set; } = true;
	public bool RequireCommunicationDetails { get; set; } = true;
	public bool RequireMedicalDetails { get; set; }
	public bool RequireFeeClearance { get; set; }
	public bool RequireStudentAccount { get; set; } = true;
	public bool RequireGuardianAccount { get; set; } = true;
	public bool RequireServiceConsent { get; set; }
	public int SeatReservationMinutes { get; set; } = 1440;
	public int SlaHours { get; set; } = 72;
	public bool IsActive { get; set; } = true;
}

public class StudentOnboardingPolicyDto : EntityDto<Guid>
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public Guid? OfferingId { get; set; }
	public Guid? StandardId { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool RequireDocuments { get; set; }
	public bool RequireFamilyDetails { get; set; }
	public bool RequireCommunicationDetails { get; set; }
	public bool RequireMedicalDetails { get; set; }
	public bool RequireFeeClearance { get; set; }
	public bool RequireStudentAccount { get; set; }
	public bool RequireGuardianAccount { get; set; }
	public bool RequireServiceConsent { get; set; }
	public int SeatReservationMinutes { get; set; }
	public int SlaHours { get; set; }
	public bool IsActive { get; set; }
}

public class CreateStudentIdentifierSeriesDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public StudentIdentifierType IdentifierType { get; set; } = StudentIdentifierType.GrNumber;
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Prefix { get; set; } = string.Empty;
	public string Suffix { get; set; } = string.Empty;
	public int Padding { get; set; } = 6;
	public long StartingSequence { get; set; } = 1;
	public long? MaximumSequence { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
}

public class StudentIdentifierSeriesDto : EntityDto<Guid>
{
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public Guid AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public StudentIdentifierType IdentifierType { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Prefix { get; set; } = string.Empty;
	public string Suffix { get; set; } = string.Empty;
	public int Padding { get; set; }
	public long NextSequence { get; set; }
	public long? MaximumSequence { get; set; }
	public DateTime EffectiveFrom { get; set; }
	public DateTime? EffectiveUntil { get; set; }
	public bool IsActive { get; set; }
	public string NextPreview { get; set; } = string.Empty;
}

public class HistoricalAdmissionReconciliationIssueDto
{
	public Guid ApplicationId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
}

public class HistoricalAdmissionReconciliationDto
{
	public int TotalHistoricalApplications { get; set; }
	public int AlreadyLinked { get; set; }
	public int ReadyToBackfill { get; set; }
	public int Backfilled { get; set; }
	public int Invalid { get; set; }
	public List<HistoricalAdmissionReconciliationIssueDto> Issues { get; set; } = [];
}
