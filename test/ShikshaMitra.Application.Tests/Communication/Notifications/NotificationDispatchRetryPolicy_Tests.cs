using System;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatchRetryPolicy_Tests
{
	[Fact]
	public void Final_automatic_attempt_preserves_provider_retry_after_for_one_reviewed_channel_retry()
	{
		var now = new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
		var row = Reject(CommunicationChannelType.WhatsApp, "WhatsAppRateLimited", now, NotificationDispatch.MaximumAutomaticAttempts);
		var delay = TimeSpan.FromHours(2);
		NotificationDispatchRetryPolicy.ScheduleAfterRejection(row, row.IoLeaseId!.Value, now, delay, now.AddHours(4), "WhatsAppRateLimited");
		row.Status.ShouldBe(NotificationDispatchState.Failed);
		row.NextAttemptAt.ShouldBe(now.Add(delay));
		var prior = FailedAttempt(row);
		NotificationDispatchRetryPolicy.CanRequestStaffRetry(row, prior).ShouldBeTrue();

		prior.SentAt = now;
		NotificationDispatchRetryPolicy.CanRequestStaffRetry(row, prior).ShouldBeFalse();

		var spent = Reject(CommunicationChannelType.WhatsApp, "WhatsAppRateLimited", now, NotificationDispatch.MaximumAttempts);
		NotificationDispatchRetryPolicy.CanRequestStaffRetry(spent, FailedAttempt(spent)).ShouldBeFalse();
	}

	[Fact]
	public void Rate_limited_legacy_attempt_without_a_saved_window_requires_review()
	{
		var now = new DateTimeOffset(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
		var row = Reject(CommunicationChannelType.WebPush, "WebPushRateLimited", now, 3);
		NotificationDispatchRetryPolicy.CanRequestStaffRetry(row, FailedAttempt(row)).ShouldBeFalse();
	}

	private static NotificationDispatch Reject(CommunicationChannelType channel, string reason, DateTimeOffset now, int number)
	{
		var tenant = Guid.NewGuid();
		var row = new NotificationDispatch(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), "{}", "test-recipient",
			channel, "test-destination", "encrypted-payload", "content-hash", "test-deduplication", now, now.AddHours(4));
		var lease = Guid.NewGuid();
		Set(row, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(row, nameof(NotificationDispatch.LeaseId), lease);
		Set(row, nameof(NotificationDispatch.LeaseExpiresAt), now.AddMinutes(5));
		Set(row, nameof(NotificationDispatch.AttemptNumber), number);
		row.BeginIo(lease, now, Guid.NewGuid());
		row.RecordOutcome(lease, now, NotificationDispatchState.Failed, errorCode: reason);
		return row;
	}

	private static DeliveryAttempt FailedAttempt(NotificationDispatch row) => new(row.DeliveryAttemptId!.Value, row.TenantId)
	{
		Status = CommunicationDeliveryStatus.Failed, ErrorCode = row.ErrorCode
	};

	private static void Set(NotificationDispatch row, string name, object value) =>
		typeof(NotificationDispatch).GetProperty(name)!.SetValue(row, value);
}
