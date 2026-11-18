using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationIntentRecovery_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
	[Fact]
	public void An_interrupted_planner_can_retry_before_expiry_without_claiming_delivery()
	{
		var row = Intent(); row.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.Automatic);
		Claim(row); var before = row.ConcurrencyStamp;
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue(); row.Status.ShouldBe(NotificationIntentState.Ready);
		row.LeaseId.ShouldBeNull(); row.LeaseExpiresAt.ShouldBeNull(); row.ClaimedAt.ShouldBeNull();
		row.ConcurrencyStamp.ShouldNotBe(before); row.PlanningCompletedAt.ShouldBeNull();
	}
	[Fact]
	public void An_old_unclassified_intent_requires_review_instead_of_becoming_sendable()
	{
		var row = Intent(); Claim(row);
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue(); row.Status.ShouldBe(NotificationIntentState.NeedsReview);
		row.ErrorCode.ShouldBe(NotificationErrorCodes.RuntimeSnapshotInvalid); row.LeaseId.ShouldBeNull();
	}
	[Fact]
	public void A_source_that_expired_during_a_planner_crash_is_not_requeued()
	{
		var row = Intent(Now.AddMinutes(5)); row.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.Automatic); Claim(row);
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue(); row.Status.ShouldBe(NotificationIntentState.Expired);
		row.ErrorCode.ShouldBe("SourceExpired");
	}
	[Fact]
	public void Recovery_does_not_steal_an_active_lease_or_reset_already_planned_work()
	{
		var row = Intent(); row.BindRuntimeSnapshot("{\"schemaVersion\":1}", NotificationIntentOrigin.Automatic); var lease = Claim(row);
		var stamp = row.ConcurrencyStamp; row.RecoverExpiredLease(Now).ShouldBeFalse(); row.ConcurrencyStamp.ShouldBe(stamp);
		row.CompletePlanning(lease, Now); stamp = row.ConcurrencyStamp;
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeFalse(); row.ConcurrencyStamp.ShouldBe(stamp);
		row.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
	}
	[Theory]
	[InlineData(NotificationIntentState.Cancelled)]
	[InlineData(NotificationIntentState.Expired)]
	[InlineData(NotificationIntentState.NeedsReview)]
	public void A_claimed_planner_can_stop_with_a_safe_reason(NotificationIntentState outcome)
	{
		var row = Intent(); var lease = Claim(row); var stamp = row.ConcurrencyStamp;
		row.StopProcessing(lease, Now, outcome, "SourceChanged"); row.Status.ShouldBe(outcome);
		row.ErrorCode.ShouldBe("SourceChanged"); row.LeaseId.ShouldBeNull(); row.ConcurrencyStamp.ShouldNotBe(stamp);
	}
	[Fact]
	public void Stale_stops_success_states_and_raw_error_payloads_are_rejected_without_mutation()
	{
		var row = Intent(); var lease = Claim(row); var stamp = row.ConcurrencyStamp;
		Should.Throw<BusinessException>(() => row.StopProcessing(Guid.NewGuid(), Now, NotificationIntentState.Cancelled, "SourceChanged"));
		Should.Throw<BusinessException>(() => row.StopProcessing(lease, Now, NotificationIntentState.Completed, "SourceChanged"));
		Should.Throw<BusinessException>(() => row.StopProcessing(lease, Now, NotificationIntentState.NeedsReview, "private@example.test"));
		row.Status.ShouldBe(NotificationIntentState.Processing); row.ConcurrencyStamp.ShouldBe(stamp); row.LeaseId.ShouldBe(lease);
	}
	private static NotificationIntent Intent(DateTimeOffset? expiry = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
		"Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(), "1", Guid.NewGuid().ToString("N"), Now, Now, "[]", "[]", expiry);
	private static Guid Claim(NotificationIntent row)
	{
		var token = Guid.NewGuid(); typeof(NotificationIntent).GetProperty(nameof(NotificationIntent.Status))!.SetValue(row, NotificationIntentState.Processing);
		typeof(NotificationIntent).GetProperty(nameof(NotificationIntent.LeaseId))!.SetValue(row, token);
		typeof(NotificationIntent).GetProperty(nameof(NotificationIntent.ClaimedAt))!.SetValue(row, Now);
		typeof(NotificationIntent).GetProperty(nameof(NotificationIntent.LeaseExpiresAt))!.SetValue(row, Now.AddMinutes(5)); return token;
	}
}
