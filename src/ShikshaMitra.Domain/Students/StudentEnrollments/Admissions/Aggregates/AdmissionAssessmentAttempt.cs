using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentAttempt : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid ApplicationId { get; private set; }
	public Guid StageInstanceId { get; private set; }
	public Guid? SessionId { get; private set; }
	public DateTime? SlotStartsAt { get; private set; }
	public DateTime? SlotEndsAt { get; private set; }
	public int AttemptNumber { get; private set; }
	public string Attendance { get; private set; } = "pending";
	public string Status { get; private set; } = "pending";
	public string Outcome { get; private set; } = "pending";
	public decimal? Marks { get; private set; }
	public string? Notes { get; private set; }
	public string? ReviewNotes { get; private set; }
	public Guid? RecordedBy { get; private set; }
	public Guid? ReviewedBy { get; private set; }
	public DateTime? ReviewedAt { get; private set; }
	public string PolicySnapshotJson { get; private set; } = string.Empty;

	protected AdmissionAssessmentAttempt() { }
	public AdmissionAssessmentAttempt(Guid id, Guid? tenantId, Guid applicationId, Guid stageId,
		Guid? sessionId, int attemptNumber, string policySnapshotJson) : base(id)
	{
		TenantId = tenantId;
		ApplicationId = applicationId;
		StageInstanceId = stageId;
		SessionId = sessionId;
		AttemptNumber = attemptNumber;
		PolicySnapshotJson = policySnapshotJson;
	}

	public void RecordAttendance(string attendance, string? notes, Guid userId, AdmissionAssessmentPolicy policy)
	{
		EnsurePending();
		if (attendance is not ("present" or "absent")) throw Invalid("Select present or absent.");
		var normalizedNotes = Normalize(notes);
		Attendance = attendance;
		Notes = normalizedNotes;
		if (attendance == "absent") RecordOutcome("absent", userId, policy);
	}
	public void AssignInterviewSlot(DateTime startsAt, DateTime endsAt)
	{
		if (!SessionId.HasValue || endsAt <= startsAt || Status != "pending") throw Invalid("Choose a valid interview slot.");
		SlotStartsAt = startsAt.ToUniversalTime(); SlotEndsAt = endsAt.ToUniversalTime();
	}

	public void RecordResult(decimal? marks, string? outcome, string? notes, Guid userId, AdmissionAssessmentPolicy policy)
	{
		EnsurePending();
		if (policy.AttendanceRequired && Attendance != "present") throw Invalid("Record attendance before the result.");
		var normalizedNotes = Normalize(notes);
		if (policy.ScoringEnabled)
		{
			if (!marks.HasValue || marks < 0 || marks > policy.TotalMarks) throw Invalid("Marks must be within the configured total.");
			Marks = marks;
			outcome = marks >= policy.PassingMarks ? "passed" : "failed";
		}
		else if (outcome is not ("passed" or "failed")) throw Invalid("Select a passed or failed result.");
		Notes = normalizedNotes;
		RecordOutcome(outcome!, userId, policy);
	}

	public void Review(bool approved, string? notes, Guid userId, DateTime now)
	{
		if (Status != "pendingReview") throw Invalid("Only a result awaiting review can be reviewed.");
		if (RecordedBy == userId) throw Invalid("A different authorized staff member must review this result.");
		ReviewNotes = approved ? Normalize(notes) : Check.NotNullOrWhiteSpace(notes, nameof(notes), 1024).Trim();
		ReviewedBy = userId;
		ReviewedAt = now;
		Status = approved ? "completed" : "pending";
		if (!approved) { Outcome = "pending"; Marks = null; Attendance = "pending"; }
	}

	public void RequestRetake(string? notes)
	{
		if (Status != "completed" || Outcome is not ("failed" or "absent")) throw Invalid("Only a failed or absent assessment can be retaken.");
		var normalizedNotes = Normalize(notes);
		Outcome = "retake";
		Notes = normalizedNotes ?? Notes;
	}

	public void Cancel(string? notes)
	{
		if (Status != "pending" || Attendance != "pending") throw Invalid("Only an unstarted attempt can be cancelled.");
		var normalizedNotes = Normalize(notes);
		Status = "cancelled";
		Notes = normalizedNotes;
	}

	public void Waive(string reason, Guid userId, DateTime now)
	{
		if (Status == "cancelled") throw Invalid("A cancelled attempt cannot be waived.");
		var normalizedReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1024).Trim();
		Outcome = "waived";
		Status = "completed";
		ReviewNotes = normalizedReason;
		ReviewedBy = userId;
		ReviewedAt = now;
	}

	private void RecordOutcome(string outcome, Guid userId, AdmissionAssessmentPolicy policy)
	{
		Outcome = outcome;
		RecordedBy = userId;
		ReviewedBy = null;
		ReviewedAt = null;
		Status = policy.ReviewRequired ? "pendingReview" : "completed";
	}
	private void EnsurePending() { if (Status != "pending") throw Invalid("This attempt is no longer open for changes."); }
	private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), 1024);
	private static BusinessException Invalid(string message) => new(AdmissionAssessmentErrorCodes.InvalidState, message);
}
