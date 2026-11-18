using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationIntentCapture_Tests
{
	[Fact]
	public void An_automatic_capture_binds_one_operation_without_rewriting_its_snapshot()
	{
		var row = Intent(); var operation = Guid.NewGuid(); row.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.Automatic);
		var before = row.ConcurrencyStamp; row.BindAutomaticOperation(operation);
		row.CampaignId.ShouldBe(operation); row.ConcurrencyStamp.ShouldNotBe(before);
		row.RuleConfigurationJson.ShouldBe("{\"schemaVersion\":1}");
		Should.Throw<BusinessException>(() => row.BindAutomaticOperation(Guid.NewGuid())); row.CampaignId.ShouldBe(operation);
	}
	[Fact]
	public void A_manual_capture_or_empty_operation_cannot_be_rebound_as_automatic()
	{
		var row = Intent(); Should.Throw<BusinessException>(() => row.BindAutomaticOperation(Guid.Empty));
		var manual = Intent(Guid.NewGuid()); var operation = Guid.NewGuid();
		manual.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.ReviewedManual, operation);
		Should.Throw<BusinessException>(() => manual.BindAutomaticOperation(Guid.NewGuid())); manual.CampaignId.ShouldBe(operation);
	}
	[Theory]
	[InlineData(NotificationIntentState.NeedsReview)]
	[InlineData(NotificationIntentState.Expired)]
	public void A_capture_issue_is_persistable_without_becoming_sendable(NotificationIntentState outcome)
	{
		var row = Intent(); var stamp = row.ConcurrencyStamp;
		row.StopBeforePlanning(outcome, "TemplateMissing"); row.Status.ShouldBe(outcome); row.ErrorCode.ShouldBe("TemplateMissing");
		row.ConcurrencyStamp.ShouldNotBe(stamp); row.LeaseId.ShouldBeNull(); row.PlanningCompletedAt.ShouldBeNull();
		Should.Throw<BusinessException>(() => row.StopBeforePlanning(NotificationIntentState.NeedsReview, "AnotherIssue"));
	}
	[Theory]
	[InlineData(NotificationIntentState.Completed)]
	[InlineData(NotificationIntentState.AwaitingDelivery)]
	[InlineData(NotificationIntentState.Processing)]
	public void Preplanning_issues_cannot_claim_progress_or_delivery(NotificationIntentState invalid)
	{
		var row = Intent(); var stamp = row.ConcurrencyStamp;
		Should.Throw<BusinessException>(() => row.StopBeforePlanning(invalid, "TemplateMissing")); row.ConcurrencyStamp.ShouldBe(stamp);
	}
	[Fact]
	public void A_capture_issue_never_stores_a_raw_destination_or_exception()
	{
		var row = Intent(); Should.Throw<BusinessException>(() => row.StopBeforePlanning(NotificationIntentState.NeedsReview, "private@example.test"));
		row.ErrorCode.ShouldBeNull();
	}
	private static NotificationIntent Intent(Guid? actor = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
		"Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(), "1", Guid.NewGuid().ToString("N"),
		DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "[]", "[]", actorUserId: actor);
	[Fact]
	public void Snapshot_binding_cannot_replace_an_already_bound_automatic_operation_with_a_manual_one()
	{
		var row = Intent(Guid.NewGuid()); var original = Guid.NewGuid(); row.BindAutomaticOperation(original);
		Should.Throw<BusinessException>(() => row.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.ReviewedManual, Guid.NewGuid()));
		row.CampaignId.ShouldBe(original);
	}
}
