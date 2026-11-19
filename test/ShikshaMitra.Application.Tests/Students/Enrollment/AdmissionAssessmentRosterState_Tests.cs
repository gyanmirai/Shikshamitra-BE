using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentRosterState_Tests
{
	private static readonly DateTime Now = new(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);
	private static AdmissionAssessmentAttempt Attempt() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 1, "{}");
	private static AdmissionAssessmentSession Session(int start, int end) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Exam", Now.AddMinutes(start), Now.AddMinutes(end), "Room", 10);
	private static readonly AdmissionAssessmentPolicy Policy = new() { ScoringEnabled = true, TotalMarks = 100, PassingMarks = 40 };
	private static void AssertState(AdmissionAssessmentAttempt? attempt, AdmissionAssessmentSession? session, string status, string attendance, string progress, bool required = true)
		=> AdmissionAssessmentRosterState.Resolve(attempt, session, Now, true, required).ShouldBe(new(status, attendance, progress));

	[Fact]
	public void Unbooked_scheduled_and_required_attendance_are_distinct()
	{
		AssertState(null, null, "notBooked", "notApplicable", "pending");
		AssertState(Attempt(), Session(1, 60), "scheduled", "notMarked", "pending");
		AssertState(Attempt(), Session(-60, 0), "attendancePending", "pending", "ongoing");
		AssertState(Attempt(), null, "resultPending", "notRequired", "ongoing", false);
	}

	[Theory]
	[InlineData(80, "passed")]
	[InlineData(20, "failed")]
	public void Results_are_visible_after_exam_end_and_do_not_advance_the_workflow(int marks, string outcome)
	{
		var attempt = Attempt();
		attempt.RecordAttendance("present", null, Guid.NewGuid(), Policy);
		AssertState(attempt, null, "resultPending", "present", "ongoing");
		attempt.RecordResult(marks, null, null, Guid.NewGuid(), Policy);
		AssertState(attempt, Session(-60, 1), "ongoing", "present", "ongoing");
		AssertState(attempt, Session(-60, 0), outcome, "present", "complete");
		typeof(AdmissionAssessmentAttempt).GetProperty(nameof(AdmissionAssessmentAttempt.Status))!.SetValue(attempt, "pendingReview");
		AssertState(attempt, null, "reviewPending", "present", "ongoing");
	}

	[Fact]
	public void Waiver_does_not_fabricate_attendance_and_keeps_previously_recorded_attendance()
	{
		var attempt = Attempt();
		attempt.Waive("Exempt", Guid.NewGuid(), Now);
		AssertState(attempt, Session(60, 120), "waived", "notRequired", "complete");
		attempt.Attendance.ShouldBe("pending");
		var attended = Attempt();
		attended.RecordAttendance("present", null, Guid.NewGuid(), Policy);
		attended.Waive("Exempt", Guid.NewGuid(), Now);
		AssertState(attended, null, "waived", "present", "complete");
	}

	[Fact]
	public void Absence_retake_cancellation_and_missing_required_attendance_do_not_claim_a_pass()
	{
		var attempt = Attempt();
		attempt.RecordAttendance("absent", null, Guid.NewGuid(), Policy);
		AssertState(attempt, null, "absent", "absent", "complete");
		attempt.RequestRetake("Retry");
		AssertState(attempt, null, "retakeRequired", "absent", "pending");
		var inconsistent = Attempt();
		inconsistent.RecordResult(80, null, null, Guid.NewGuid(), Policy);
		AssertState(inconsistent, null, "attendancePending", "pending", "ongoing");
	}
}
