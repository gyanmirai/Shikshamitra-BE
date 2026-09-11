using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionAssessmentAttempt_Tests
{
    private static readonly Guid Recorder = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Reviewer = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Now = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, "failed")]
    [InlineData(39, "failed")]
    [InlineData(40, "passed")]
    [InlineData(100, "passed")]
    public void Recorded_Score_Determines_Outcome_Regardless_Of_Client_Outcome(int score, string outcome)
    {
        var attempt = Attempt();
        attempt.RecordResult(score, outcome == "passed" ? "failed" : "passed", "  result  ", Recorder, Policy());
        attempt.Marks.ShouldBe(score);
        attempt.Outcome.ShouldBe(outcome);
        attempt.Status.ShouldBe("completed");
        attempt.RecordedBy.ShouldBe(Recorder);
        attempt.Notes.ShouldBe("result");
        Should.Throw<BusinessException>(() => attempt.RecordResult(50, null, null, Recorder, Policy()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Scored_Attempt_Requires_Marks_Within_Total(int? marks)
    {
        var attempt = Attempt();
        Should.Throw<BusinessException>(() => attempt.RecordResult(marks, "passed", null, Recorder, Policy()));
        attempt.Status.ShouldBe("pending");
        attempt.Marks.ShouldBeNull();
    }

    [Fact]
    public void Attendance_Is_Required_Independently_Of_Scheduling()
    {
        var policy = Policy() with { AttendanceRequired = true, SchedulingEnabled = false };
        var attempt = Attempt();
        Should.Throw<BusinessException>(() => attempt.RecordResult(80, null, null, Recorder, policy));
        attempt.RecordAttendance("present", null, Recorder, policy);
        attempt.RecordResult(80, null, null, Recorder, policy);
        attempt.Outcome.ShouldBe("passed");
    }

    [Fact]
    public void Review_Requires_Another_Staff_Member_And_Rejection_Allows_Correction()
    {
        var policy = Policy() with { ReviewRequired = true };
        var attempt = Attempt();
        attempt.RecordResult(50, null, null, Recorder, policy);
        attempt.Status.ShouldBe("pendingReview");
        Should.Throw<BusinessException>(() => attempt.Review(true, null, Recorder, Now));
        Should.Throw<ArgumentException>(() => attempt.Review(false, " ", Reviewer, Now));
        attempt.Review(false, "Check the answer sheet", Reviewer, Now);
        attempt.Status.ShouldBe("pending");
        attempt.Outcome.ShouldBe("pending");
        attempt.Marks.ShouldBeNull();
        attempt.RecordResult(60, null, "Corrected", Recorder, policy);
        attempt.ReviewedBy.ShouldBeNull();
        attempt.Review(true, "Verified", Reviewer, Now);
        attempt.Status.ShouldBe("completed");
        attempt.ReviewedBy.ShouldBe(Reviewer);
        attempt.ReviewedAt.ShouldBe(Now);
    }

    [Fact]
    public void Absent_Outcome_Can_Be_Retaken_But_Passing_Result_Cannot()
    {
        var attempt = Attempt();
        attempt.RecordAttendance("absent", "Absent", Recorder, Policy());
        attempt.Outcome.ShouldBe("absent");
        attempt.RequestRetake("Another opportunity");
        attempt.Outcome.ShouldBe("retake");
        var passed = Attempt();
        passed.RecordResult(40, null, null, Recorder, Policy());
        Should.Throw<BusinessException>(() => passed.RequestRetake(null));
    }

    [Fact]
    public void Only_Unstarted_Attempts_Can_Be_Cancelled()
    {
        var cancelled = Attempt();
        cancelled.Cancel("Changed session");
        cancelled.Status.ShouldBe("cancelled");
        Should.Throw<BusinessException>(() => cancelled.RecordAttendance("present", null, Recorder, Policy()));
        Should.Throw<BusinessException>(() => cancelled.Waive("Exception", Reviewer, Now));
        var started = Attempt();
        started.RecordAttendance("present", null, Recorder, Policy());
        Should.Throw<BusinessException>(() => started.Cancel(null));
    }

    [Fact]
    public void Waiver_Records_The_Authorizing_Staff_And_Reason()
    {
        var attempt = Attempt();
        attempt.Waive("  Transfer student exception  ", Reviewer, Now);
        attempt.Status.ShouldBe("completed");
        attempt.Outcome.ShouldBe("waived");
        attempt.ReviewNotes.ShouldBe("Transfer student exception");
        attempt.ReviewedBy.ShouldBe(Reviewer);
        attempt.ReviewedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("retake")]
    public void Non_Scored_Results_Still_Require_A_Pass_Or_Fail_Outcome(string outcome)
    {
        Should.Throw<BusinessException>(() =>
            Attempt().RecordResult(null, outcome, null, Recorder, Policy() with { ScoringEnabled = false }));
    }

    private static AdmissionAssessmentAttempt Attempt() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 1, "{\"version\":1}");

    private static AdmissionAssessmentPolicy Policy() => new()
    {
        Version = 1, Enabled = true, ScoringEnabled = true,
        TotalMarks = 100, PassingMarks = 40, MaxAttempts = 2
    };
}
