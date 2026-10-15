using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentProgress_Tests
{
	private static readonly DateTime Now = new(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc);
	private static AdmissionAssessmentAttempt Attempt(Guid? sessionId = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), sessionId, 1, "{}");
	private static AdmissionAssessmentSession Session(int start, int end) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Exam", Now.AddMinutes(start), Now.AddMinutes(end), "Room", 10);
	private static readonly AdmissionAssessmentPolicy Policy = new() {ScoringEnabled = true, TotalMarks = 100, PassingMarks = 40};

	[Fact]
	public void Unbooked_future_started_and_ended_without_result_have_no_status_gap()
	{
		AdmissionAssessmentRosterService.ClassifyStatus(null, null, Now, true).ShouldBe("pending");
		var attempt = Attempt();
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, Session(1, 60), Now, true).ShouldBe("pending");
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, Session(0, 60), Now, true).ShouldBe("ongoing");
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, Session(-60, 0), Now, true).ShouldBe("ongoing");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(80)]
	public void Saved_marks_require_exam_end_but_do_not_require_workflow_advancement(int marks)
	{
		var attempt = Attempt();
		attempt.RecordResult(marks, null, null, Guid.NewGuid(), Policy);
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, Session(-60, 1), Now, true).ShouldBe("ongoing");
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, Session(-60, 0), Now, true).ShouldBe("complete");
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, null, Now, true).ShouldBe("complete");
	}

	[Fact]
	public void Interview_slot_times_take_precedence_over_the_shared_session_window()
	{
		var session = Session(-60, 60);
		var attempt = Attempt(session.Id);
		attempt.AssignInterviewSlot(Now.AddMinutes(-15), Now);
		attempt.RecordResult(80, null, null, Guid.NewGuid(), Policy);
		AdmissionAssessmentRosterService.ClassifyStatus(attempt, session, Now, true).ShouldBe("complete");
	}

	[Fact]
	public void Waived_absent_qualitative_and_retake_outcomes_remain_reachable()
	{
		var waived = Attempt();
		waived.Waive("Approved exemption", Guid.NewGuid(), Now);
		AdmissionAssessmentRosterService.ClassifyStatus(waived, Session(60, 120), Now, true).ShouldBe("complete");
		var absent = Attempt();
		absent.RecordAttendance("absent", null, Guid.NewGuid(), Policy);
		AdmissionAssessmentRosterService.ClassifyStatus(absent, Session(-60, 0), Now, true).ShouldBe("complete");
		absent.RequestRetake("New attempt requested");
		AdmissionAssessmentRosterService.ClassifyStatus(absent, Session(-60, 0), Now, true).ShouldBe("pending");
		var qualitative = Attempt();
		qualitative.RecordResult(null, "passed", null, Guid.NewGuid(), Policy with {ScoringEnabled = false});
		AdmissionAssessmentRosterService.ClassifyStatus(qualitative, null, Now, false).ShouldBe("complete");
		AdmissionAssessmentRosterService.ClassifyStatus(qualitative, null, Now, true).ShouldBe("ongoing");
	}
}
