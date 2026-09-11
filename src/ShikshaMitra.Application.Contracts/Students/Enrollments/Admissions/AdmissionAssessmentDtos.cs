using System;
using System.Collections.Generic;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentScopeDto
{
	public Guid AdmissionOfferingId { get; set; }
	public string OfferingName { get; set; } = string.Empty;
	public Guid WorkflowStageDefinitionId { get; set; }
	public string StageName { get; set; } = string.Empty;
	public Guid WorkflowDefinitionId { get; set; }
	public int WorkflowVersion { get; set; }
	public bool SchedulingEnabled { get; set; }
}

public class AdmissionAssessmentSessionInput
{
	public Guid AdmissionOfferingId { get; set; }
	public Guid WorkflowStageDefinitionId { get; set; }
	public string Name { get; set; } = string.Empty;
	public DateTime StartsAt { get; set; }
	public DateTime EndsAt { get; set; }
	public string Location { get; set; } = string.Empty;
	public int Capacity { get; set; }
	public string? ConcurrencyStamp { get; set; }
}

public class AdmissionAssessmentSessionDto : AdmissionAssessmentSessionInput
{
	public Guid Id { get; set; }
	public int BookedCount { get; set; }
	public bool IsCancelled { get; set; }
}

public class AdmissionAssessmentAttemptDto
{
	public Guid Id { get; set; }
	public int AttemptNumber { get; set; }
	public Guid? SessionId { get; set; }
	public string? SessionName { get; set; }
	public DateTime? SessionStartsAt { get; set; }
	public DateTime? SessionEndsAt { get; set; }
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
	public List<AdmissionAssessmentAttemptDto> Attempts { get; set; } = [];
	public string ConcurrencyStamp { get; set; } = string.Empty;
	public string? Outcome { get; set; }
	public List<string> Blockers { get; set; } = [];
	public bool CanComplete { get; set; }
	public bool CanBook { get; set; }
	public bool CanRecordAttendance { get; set; }
	public bool CanRecordResult { get; set; }
	public bool CanReview { get; set; }
	public bool CanRetake { get; set; }
	public bool CanWaive { get; set; }
	public bool CanCancel { get; set; }
}

public class AdmissionAssessmentActionInput
{
	public string? ConcurrencyStamp { get; set; }
	public string? Notes { get; set; }
}
public class AdmissionAssessmentBookingInput : AdmissionAssessmentActionInput { public Guid? SessionId { get; set; } }
public class AdmissionAssessmentAttendanceInput : AdmissionAssessmentActionInput { public string Attendance { get; set; } = string.Empty; }
public class AdmissionAssessmentResultInput : AdmissionAssessmentActionInput { public decimal? Marks { get; set; } public string? Outcome { get; set; } }
public class AdmissionAssessmentReviewInput : AdmissionAssessmentActionInput { public bool Approved { get; set; } }
public class AdmissionAssessmentWaiverInput : AdmissionAssessmentActionInput { public string Reason { get; set; } = string.Empty; }
