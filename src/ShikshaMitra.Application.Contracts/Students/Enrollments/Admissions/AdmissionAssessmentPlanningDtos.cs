using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentSessionScopeInput
{
	public Guid AdmissionOfferingId { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
}

public class AdmissionAssessmentSessionListInput : PagedAndSortedResultRequestDto
{
	public Guid? AdmissionOfferingId { get; set; }
	public Guid? WorkflowStageDefinitionId { get; set; }
	public Guid? AcademicYearId { get; set; }
	public Guid? BoardId { get; set; }
	public Guid? MediumId { get; set; }
	public Guid? StandardId { get; set; }
	[MaxLength(800)] public string? StandardIds { get; set; }
	[MaxLength(128)] public string? Search { get; set; }
	public bool IncludeCancelled { get; set; } = true;
}

public class AdmissionAssessmentSessionPreviewInput : PagedResultRequestDto
{
	public Guid? SessionId { get; set; }
	public string? SessionConcurrencyStamp { get; set; }
	[Required] public AdmissionAssessmentSessionInput Session { get; set; } = new();
	[MaxLength(128)] public string? Search { get; set; }
	/// <summary>Optional student selection, intersected with authorized eligible assessment stages.</summary>
	[MaxLength(200)] public List<Guid>? StageInstanceIds { get; set; }
}

public class AdmissionAssessmentBookingCandidateDto
{
	public Guid ApplicantId { get; set; }
	public Guid ApplicationId { get; set; }
	public Guid StageInstanceId { get; set; }
	public string ApplicationNumber { get; set; } = string.Empty;
	public string ApplicantName { get; set; } = string.Empty;
	public Guid AdmissionOfferingId { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public DateTimeOffset? SlotStartsAt { get; set; }
	public DateTimeOffset? SlotEndsAt { get; set; }
}

public class AdmissionAssessmentInterviewSlotDto
{
	public DateTimeOffset StartsAt { get; set; }
	public DateTimeOffset EndsAt { get; set; }
}

public class AdmissionAssessmentSessionPreviewDto : PagedResultDto<AdmissionAssessmentBookingCandidateDto>
{
	public int EligibleCount { get; set; }
	public int AvailableCount { get; set; }
	public int RemainingCount { get; set; }
	public bool RequiresRoomApproval { get; set; }
	public string RoomName { get; set; } = string.Empty;
	public string BuildingName { get; set; } = string.Empty;
	public string FloorName { get; set; } = string.Empty;
	public string PreviewFingerprint { get; set; } = string.Empty;
	public List<AdmissionAssessmentInterviewSlotDto> Slots { get; set; } = [];
	public List<AdmissionAssessmentBookingCandidateDto> ProposedBookings { get; set; } = [];
	public List<string> Warnings { get; set; } = [];
}

public class AdmissionAssessmentSelectedBookingInput
{
	public Guid ApplicationId { get; set; }
	public Guid StageInstanceId { get; set; }
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
	public DateTimeOffset? SlotStartsAt { get; set; }
}

public class AdmissionAssessmentSessionConfirmInput
{
	public Guid? SessionId { get; set; }
	public string? SessionConcurrencyStamp { get; set; }
	[Required] public AdmissionAssessmentSessionInput Session { get; set; } = new();
	public Guid IdempotencyKey { get; set; }
	[Required, MaxLength(64)] public string PreviewFingerprint { get; set; } = string.Empty;
	[Required, MaxLength(200)] public List<AdmissionAssessmentSelectedBookingInput> Bookings { get; set; } = [];
}

public class AdmissionAssessmentSessionConfirmationDto
{
	public AdmissionAssessmentSessionDto Session { get; set; } = new();
	public int BookedCount { get; set; }
}

public class AdmissionAssessmentStaffDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
}

public class AdmissionAssessmentRoomDto
{
	public Guid Id { get; set; }
	public string RoomName { get; set; } = string.Empty;
	public Guid BuildingId { get; set; }
	public string BuildingName { get; set; } = string.Empty;
	public Guid FloorId { get; set; }
	public string FloorName { get; set; } = string.Empty;
	public int Capacity { get; set; }
}

public class AdmissionAssessmentSessionResourcesDto
{
	public List<AdmissionAssessmentRoomDto> Rooms { get; set; } = [];
	public List<AdmissionAssessmentStaffDto> Interviewers { get; set; } = [];
}
