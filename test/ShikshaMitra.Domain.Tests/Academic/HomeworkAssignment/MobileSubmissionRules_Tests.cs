using System;
using ShikshaMitra.Academic.Mobile;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class MobileSubmissionRules_Tests
{
	[Theory]
	[InlineData(SubmissionType.InPersonSubmission)]
	[InlineData(SubmissionType.NoSubmissionRequired)]
	public void Completion_requires_acknowledgment_and_files_are_optional(SubmissionType type)
	{
		var due = DateTime.UtcNow.AddDays(-1);
		MobileSubmissionRules.Validate(null, due, DateTime.UtcNow, false, type, true, false, null, 0);
		Should.Throw<BusinessException>(() => MobileSubmissionRules.Validate(null, due, DateTime.UtcNow, false, type, false, false, null, 1))
			.Code.ShouldBe(MobileSubmissionErrorCodes.AcknowledgmentRequired);
		MobileSubmissionRules.Validate(null, due, DateTime.UtcNow, false, type, false, false, null, 0, true);
		MobileSubmissionRules.Validate(null, due, DateTime.UtcNow, false, type, false, false, null, 1, true);
		var submission = new StudentSubmissionEntity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), due);
		submission.SaveMobileResponse(null, "[]", null, null, false, DateTimeOffset.UtcNow, acknowledged: true);
		submission.Acknowledged.ShouldBeTrue(); submission.SubmissionDate.ShouldNotBeNull();
		Should.Throw<BusinessException>(() => MobileSubmissionRules.Validate(submission, due, DateTime.UtcNow, false, type, false, true, null, 0, true));
	}
	[Fact]
	public void Mobile_timestamps_round_trip_at_microsecond_precision_and_keep_the_first_submission()
	{
		var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero).AddTicks(7);
		var submission = new StudentSubmissionEntity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.UtcDateTime.AddDays(1));
		submission.SaveMobileResponse("First", "[]", null, null, false, now);
		submission.SubmissionDate!.Value.UtcTicks.ShouldBe(now.UtcTicks - 7);
		var first = submission.SubmissionDate;
		submission.SaveMobileResponse("Edit", "[]", null, null, false, now.AddSeconds(1));
		submission.SubmissionDate.ShouldBe(first);
		submission.MobileUpdatedAt!.Value.ShouldBeGreaterThan(first!.Value);
	}
	[Fact]
	public void First_submission_is_allowed_after_due_time_but_replacements_are_locked()
	{
		var due = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
		MobileSubmissionRules.Validate(null, due, due, false, SubmissionType.OnlineText, false, false, "Answer", 0);
		MobileSubmissionRules.Validate(null, due, due.AddDays(1), false, SubmissionType.OnlineText, false, false, "Answer", 0);
		var submission = new StudentSubmissionEntity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), due);
		submission.SaveMobileResponse("Draft", "[]", null, null, true, new DateTimeOffset(due.AddDays(1)));
		MobileSubmissionRules.CanSubmit(submission, false).ShouldBeTrue();
		MobileSubmissionRules.Validate(submission, due, due.AddDays(1), false, SubmissionType.OnlineText, false, false, "Answer", 0);
		submission.SaveMobileResponse("Final", "[]", null, null, false, new DateTimeOffset(due));
		MobileSubmissionRules.CanSubmit(submission, false).ShouldBeFalse();
		MobileSubmissionRules.Validate(submission, due, due, false, SubmissionType.OnlineText, false, true, "Edit", 0);
		Should.Throw<BusinessException>(() => MobileSubmissionRules.Validate(submission, due, due.AddTicks(1), false, SubmissionType.OnlineText, false, true, "Edit", 0));
		Should.Throw<BusinessException>(() => MobileSubmissionRules.Validate(submission, due, due, false, SubmissionType.OnlineText, false, false, "Second submission", 0));
		Should.Throw<BusinessException>(() => MobileSubmissionRules.Validate(null, due, due.AddMinutes(-1), true, SubmissionType.OnlineText, true, false, null, 0));
	}
	[Fact]
	public void Reviewing_or_finalizing_a_response_prevents_draft_reversion()
	{
		var due = DateTime.UtcNow.AddDays(1);
		var submission = new StudentSubmissionEntity(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), due);
		submission.RecordSubmission("Final");
		Should.Throw<BusinessException>(() => MobileSubmissionRules.Validate(submission, due, DateTime.UtcNow, false, SubmissionType.OnlineText, true, false, "Draft", 0));
		submission.UpdateFeedback(10, 10, "Reviewed", null, SubmissionStatus.Reviewed, Guid.NewGuid());
		submission.Status = SubmissionStatus.Draft;
		MobileSubmissionRules.CanEdit(submission, due, DateTime.UtcNow, false).ShouldBeFalse();
	}
}
