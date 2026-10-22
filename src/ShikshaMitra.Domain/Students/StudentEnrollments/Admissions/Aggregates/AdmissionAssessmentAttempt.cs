using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentAttempt : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const string SubjectMarksEnteredProperty = "AdmissionAssessmentSubjectMarksEntered";
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
	public string? SubjectScoresJson { get; private set; }

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
		policy = SnapshotPolicy(policy);
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
		policy = SnapshotPolicy(policy);
		EnsurePending();
		if (policy.AttendanceRequired && Attendance != "present") throw Invalid("Record attendance before the result.");
		if (policy.ScoringMode == "subjects") throw Invalid("Enter all subject marks before submitting this result.");
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

	public IReadOnlyList<AdmissionAssessmentSubjectScore> GetSubjectScores() => string.IsNullOrWhiteSpace(SubjectScoresJson)
		? [] : JsonSerializer.Deserialize<List<AdmissionAssessmentSubjectScore>>(SubjectScoresJson) ?? [];
	public bool HasEnteredSubjectMarks() => this.GetProperty<bool>(SubjectMarksEnteredProperty) || GetSubjectScores().Any(score => score.Marks.HasValue);

	public void SaveResultDraft(IReadOnlyCollection<AdmissionAssessmentSubjectScore> scores, string? notes, AdmissionAssessmentPolicy policy)
	{
		policy = SnapshotPolicy(policy);
		EnsureResultEntry(policy);
		var normalized = ValidateSubjectScores(scores, policy, false);
		var normalizedNotes = Normalize(notes);
		if (HasEnteredSubjectMarks() || normalized.Any(score => score.Marks.HasValue)) this.SetProperty(SubjectMarksEnteredProperty, true);
		SubjectScoresJson = JsonSerializer.Serialize(normalized);
		Notes = normalizedNotes;
	}

	public void RecordSubjectResult(IReadOnlyCollection<AdmissionAssessmentSubjectScore> scores, string? notes, Guid userId, AdmissionAssessmentPolicy policy)
	{
		policy = SnapshotPolicy(policy);
		EnsureResultEntry(policy);
		var normalized = ValidateSubjectScores(scores, policy, true);
		var normalizedNotes = Normalize(notes);
		var profile = policy.EffectiveProfile!;
		var total = normalized.Sum(score => score.Marks!.Value);
		var subjectPassed = profile.Subjects.All(subject => normalized.Single(score => score.Code == subject.Code).Marks >= subject.PassingMarks.GetValueOrDefault());
		var passed = profile.PassRule switch { "overall" => total >= profile.PassingMarks, "everySubject" => subjectPassed, _ => subjectPassed && total >= profile.PassingMarks };
		this.SetProperty(SubjectMarksEnteredProperty, true);
		SubjectScoresJson = JsonSerializer.Serialize(normalized);
		Marks = total;
		Notes = normalizedNotes;
		RecordOutcome(passed ? "passed" : "failed", userId, policy);
	}

	public bool HasCompleteSubjectScores(AdmissionAssessmentPolicy policy)
	{
		policy = SnapshotPolicy(policy);
		if (policy.ScoringMode != "subjects") return false;
		try { ValidateSubjectScores(GetSubjectScores().ToList(), policy, true); return true; }
		catch (BusinessException) { return false; }
	}

	private void EnsureResultEntry(AdmissionAssessmentPolicy policy)
	{
		EnsurePending();
		if (policy.ScoringMode != "subjects" || policy.EffectiveProfile == null) throw Invalid("Choose a valid class assessment profile before entering subject marks.");
		if (policy.AttendanceRequired && Attendance != "present") throw Invalid("Record attendance before the result.");
	}

	private AdmissionAssessmentPolicy SnapshotPolicy(AdmissionAssessmentPolicy fallback)
	{
		try { return AdmissionAssessmentPolicy.Read(PolicySnapshotJson) is { } policy ? policy with { ReviewRequired = false } : fallback; }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionAssessmentErrorCodes.InvalidPolicy, exception.Message); }
	}

	private static List<AdmissionAssessmentSubjectScore> ValidateSubjectScores(IReadOnlyCollection<AdmissionAssessmentSubjectScore> scores, AdmissionAssessmentPolicy policy, bool complete)
	{
		var subjects = policy.EffectiveProfile?.Subjects ?? throw Invalid("The class assessment profile is missing.");
		if (scores == null || scores.Count > subjects.Count || scores.Any(score => score == null || string.IsNullOrWhiteSpace(score.Code)) ||
			scores.Select(score => score.Code).Distinct(StringComparer.Ordinal).Count() != scores.Count || scores.Any(score => !subjects.Any(subject => subject.Code == score.Code)))
			throw Invalid("Use each configured assessment subject once.");
		var normalized = subjects.Select(subject => new AdmissionAssessmentSubjectScore { Code = subject.Code, Marks = scores.SingleOrDefault(score => score.Code == subject.Code)?.Marks }).ToList();
		foreach (var score in normalized)
		{
			var subject = subjects.Single(item => item.Code == score.Code);
			if (score.Marks < 0 || score.Marks > subject.TotalMarks || complete && !score.Marks.HasValue)
				throw Invalid($"{subject.Name}: enter marks from zero to {subject.TotalMarks} before submitting.");
		}
		return normalized;
	}

	public void Review(bool approved, string? notes, Guid userId, DateTime now)
	{
		if (Status != "pendingReview") throw Invalid("Only a result awaiting review can be reviewed.");
		if (RecordedBy == userId) throw Invalid("A different authorized staff member must review this result.");
		ReviewNotes = approved ? Normalize(notes) : Check.NotNullOrWhiteSpace(notes, nameof(notes), 1024).Trim();
		ReviewedBy = userId;
		ReviewedAt = now;
		Status = approved ? "completed" : "pending";
		if (!approved) { Outcome = "pending"; Marks = null; SubjectScoresJson = null; Attendance = "pending"; }
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
		if (Status != "pending" || Attendance != "pending" || HasEnteredSubjectMarks()) throw Invalid("Only an unstarted attempt can be cancelled.");
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
