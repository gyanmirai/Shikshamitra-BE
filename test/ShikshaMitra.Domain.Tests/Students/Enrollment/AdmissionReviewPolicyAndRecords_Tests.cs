using System;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionReviewPolicyAndRecords_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("{}")]
	[InlineData("{\"review\":{\"enabled\":false}}")]
	public void Review_basic_and_disabled_configuration_remains_legacy(string? json) => AdmissionReviewPolicy.Read(json).ShouldBeNull();
	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void Review_future_or_missing_versions_are_not_silently_accepted(int version) =>
		Should.Throw<ArgumentException>(() => (Policy() with { Version = version }).Validate());
	[Theory]
	[InlineData(0)]
	[InlineData(366)]
	public void Review_deadline_policy_is_bounded(int days) => Should.Throw<ArgumentException>(() => (Policy() with { DueInDays = days }).Validate());
	[Fact]
	public void Review_policy_rejects_duplicate_codes_and_sources_and_unknown_kinds()
	{
		Should.Throw<ArgumentException>(() => (Policy() with { Checklist = [new() { Code = "A", Name = "A" }, new() { Code = "A", Name = "Another" }] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { Prerequisites = [new() { Kind = "assessment", SourceStageCode = "EXAM" }, new() { Kind = "assessment", SourceStageCode = "exam" }] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { Prerequisites = [new() { Kind = "payment", SourceStageCode = "PAY" }] }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { ReviewerRole = new string('R', 257) }).Validate());
		Should.Throw<ArgumentException>(() => (Policy() with { AssignedReviewerId = Guid.Empty }).Validate());
		(Policy() with { DueInDays = 365, Checklist = Enumerable.Range(1, 30).Select(index => new AdmissionReviewChecklistItem { Code = $"ITEM{index}", Name = $"Item {index}" }).ToList() }).Validate();
	}
	[Theory]
	[InlineData(null, "Because")]
	[InlineData("approve", null)]
	[InlineData("approve", " ")]
	[InlineData("unknown", "Because")]
	public void Review_submission_requires_a_real_recommendation_and_reason(string? recommendation, string? reason) =>
		Should.Throw<BusinessException>(() => Record("submitted", recommendation, reason)).Code.ShouldBe(AdmissionReviewErrorCodes.InvalidInput);
	[Fact]
	public void Review_record_snapshots_and_actor_times_are_immutable()
	{
		var record = Record("submitted", "approve", "  Complete evidence  ");
		record.Reason.ShouldBe("Complete evidence"); record.RecordedAt.Kind.ShouldBe(DateTimeKind.Utc);
		typeof(AdmissionReviewRecord).GetProperties().Where(item => item.DeclaringType == typeof(AdmissionReviewRecord))
			.ShouldAllBe(item => item.SetMethod == null || !item.SetMethod.IsPublic);
		Record("draft", null, null).Status.ShouldBe("draft");
	}
	[Theory]
	[InlineData("reject")]
	[InlineData("waitlist")]
	[InlineData("requestCorrection")]
	public void Review_non_approval_decisions_require_reasons(string decision) =>
		Should.Throw<BusinessException>(() => new AdmissionReviewDecision(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), decision, " ", Guid.NewGuid(), DateTime.UtcNow));
	[Fact]
	public void Review_work_item_preserves_past_deadlines_and_rejects_invalid_state_without_mutation()
	{
		var due = DateTime.UtcNow.AddDays(-2);
		var work = new AdmissionReviewWorkItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, due);
		work.DueAt.ShouldBe(due); work.Status.ShouldBe("notStarted");
		Should.Throw<BusinessException>(() => work.Record("unexpected")); work.Status.ShouldBe("notStarted");
		Should.Throw<BusinessException>(() => work.Assign(Guid.Empty, null)); work.DueAt.ShouldBe(due);
	}
	private static AdmissionReviewPolicy Policy() => new() { Version = 1, Enabled = true };
	private static AdmissionReviewRecord Record(string status, string? recommendation, string? reason) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
		status, recommendation, reason, null, null, null, new string('A', 64), "{}", "[]", "[]", Guid.NewGuid(), DateTime.UtcNow);
}
