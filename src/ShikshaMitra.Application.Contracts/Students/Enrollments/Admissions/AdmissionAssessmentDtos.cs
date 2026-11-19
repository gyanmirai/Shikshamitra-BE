using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentScopeDto
{
	public string ClassName { get; set; } = string.Empty;
	public Guid AdmissionOfferingId { get; set; }
	public Guid BoardId { get; set; }
	public Guid MediumId { get; set; }
	public Guid StandardId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string OfferingName { get; set; } = string.Empty;
	public string? OfferingCode { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
	public string StageName { get; set; } = string.Empty;
	public Guid WorkflowDefinitionId { get; set; }
	public string? WorkflowName { get; set; }
	public int WorkflowVersion { get; set; }
	public bool SchedulingEnabled { get; set; }
}

public class AdmissionAssessmentSessionInput
{
	[Required, MaxLength(20)] public List<AdmissionAssessmentSessionScopeInput> Scopes { get; set; } = [];
	[Required, RegularExpression("^(exam|individualInterview|groupInterview)$")] public string SessionType { get; set; } = "exam";
	public Guid? RoomId { get; set; }
	public Guid? InterviewerId { get; set; }
	public int? SlotDurationMinutes { get; set; }
	public int? BreakMinutes { get; set; }
	public Guid AdmissionOfferingId { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
	[Required, MaxLength(128)] public string Name { get; set; } = string.Empty;
	public DateTimeOffset StartsAt { get; set; }
	public DateTimeOffset EndsAt { get; set; }
	[MaxLength(256)] public string Location { get; set; } = string.Empty;
	[Range(1, 10000)] public int Capacity { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class AdmissionAssessmentSessionDto : AdmissionAssessmentSessionInput
{
	public bool CanManage { get; set; } = true;
	public List<AdmissionAssessmentScopeDto> ScopeDetails { get; set; } = [];
	public Guid? RoomReservationId { get; set; }
	public int? RoomReservationStatus { get; set; }
	public string? InterviewerName { get; set; }
	public Guid Id { get; set; }
	public int BookedCount { get; set; }
	public bool IsCancelled { get; set; }
}

public class AdmissionAssessmentAttemptDto
{
	public AdmissionAssessmentClassProfile? EffectiveProfile { get; set; }
	public List<AdmissionAssessmentSubjectScore> SubjectScores { get; set; } = [];
	public DateTimeOffset? SlotStartsAt { get; set; }
	public DateTimeOffset? SlotEndsAt { get; set; }
	public Guid Id { get; set; }
	public int AttemptNumber { get; set; }
	public Guid? SessionId { get; set; }
	public string? SessionName { get; set; }
	public DateTimeOffset? SessionStartsAt { get; set; }
	public DateTimeOffset? SessionEndsAt { get; set; }
	public string? SessionLocation { get; set; }
	public string Attendance { get; set; } = "pending";
	public string Status { get; set; } = "pending";
	public string Outcome { get; set; } = "pending";
	public decimal? Marks { get; set; }
	public string? Notes { get; set; }
	public string? ReviewNotes { get; set; }
	public Guid? RecordedBy { get; set; }
	public Guid? ReviewedBy { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class AdmissionAssessmentDto
{
	public Guid ApplicationId { get; set; }
	public Guid StageInstanceId { get; set; }
	public bool AssessmentEnabled { get; set; }
	public AdmissionAssessmentPolicy? Policy { get; set; }
	public AdmissionAssessmentClassProfile? EffectiveProfile { get; set; }
	public List<AdmissionAssessmentAttemptDto> Attempts { get; set; } = [];
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string? Outcome { get; set; }
	public List<string> Blockers { get; set; } = [];
	public bool CanComplete { get; set; }
	public bool CanBook { get; set; }
	public bool CanRecordAttendance { get; set; }
	public bool CanRecordResult { get; set; }
	public bool CanSaveResultDraft { get; set; }
	public bool CanSubmitResult { get; set; }
	public bool CanReview { get; set; }
	public bool CanRetake { get; set; }
	public bool CanWaive { get; set; }
	public bool CanCancel { get; set; }
}

public class AdmissionAssessmentActionInput
{
	public bool CancelPendingBookings { get; set; }
	public string? ConcurrencyStamp { get; set; }
	public string? Notes { get; set; }
}
public class AdmissionAssessmentBookingInput : AdmissionAssessmentActionInput { public Guid? SessionId { get; set; } public DateTimeOffset? SlotStartsAt { get; set; } }
public class AdmissionAssessmentAttendanceInput : AdmissionAssessmentActionInput { public string Attendance { get; set; } = string.Empty; }
public class AdmissionAssessmentResultInput : AdmissionAssessmentActionInput
{
	public decimal? Marks { get; set; }
	public string? Outcome { get; set; }
	public List<AdmissionAssessmentSubjectScore> SubjectScores { get; set; } = [];
}
public class AdmissionAssessmentReviewInput : AdmissionAssessmentActionInput { public bool Approved { get; set; } }
public class AdmissionAssessmentWaiverInput : AdmissionAssessmentActionInput { public string Reason { get; set; } = string.Empty; }
