using System;
using System.Buffers.Binary;

namespace ShikshaMitra.Communication.Notifications;

internal static class NotificationDispatchRetryPolicy
{
	private static readonly int[] RetryMinutes = [1, 5, 15, 60];

	internal static bool AllowsStaffRetry(CommunicationCampaignStatus status) =>
		status is not (CommunicationCampaignStatus.Cancelled or CommunicationCampaignStatus.PendingApproval);

	internal static bool AllowsStaffRetry(NotificationIntentState status, DateTimeOffset? planningCompletedAt) =>
		planningCompletedAt.HasValue && status is (NotificationIntentState.AwaitingDelivery or NotificationIntentState.Completed or
			NotificationIntentState.PartiallyFailed or NotificationIntentState.NeedsReview);

	internal static bool CanRequestStaffRetry(NotificationDispatch dispatch, DeliveryAttempt attempt) =>
		dispatch.CanRetryDefiniteRejection && attempt.Id == dispatch.DeliveryAttemptId &&
		attempt.Status == CommunicationDeliveryStatus.Failed && attempt.SentAt == null && attempt.DeliveredAt == null &&
		attempt.ReadAt == null && attempt.ProviderMessageId == null && attempt.ErrorCode == dispatch.ErrorCode &&
		IsKnownTransient(dispatch) && (dispatch.Channel == CommunicationChannelType.Email || dispatch.NextAttemptAt > dispatch.IoStartedAt);

	internal static void ScheduleAfterRejection(NotificationDispatch dispatch, Guid lease, DateTimeOffset now,
		TimeSpan? retryAfter, DateTimeOffset? expiry, string reason)
	{
		if (!dispatch.CanRetryDefiniteRejection) return;
		var delay = Backoff(dispatch, retryAfter);
		if (delay >= DateTimeOffset.MaxValue - now) return;
		var due = now.Add(delay);
		if (dispatch.ExpiresAt <= due || expiry <= due) return;

		if (dispatch.AttemptNumber < NotificationDispatch.MaximumAutomaticAttempts)
			dispatch.ScheduleRetry(lease, now, due, reason);
		else if (IsKnownTransient(dispatch))
			dispatch.PreserveRetryWindow(due);
	}

	private static bool IsKnownTransient(NotificationDispatch dispatch) => (dispatch.Channel, dispatch.ErrorCode) switch
	{
		(CommunicationChannelType.Email, "SmtpTemporaryRejection") => true,
		(CommunicationChannelType.WhatsApp, "WhatsAppRateLimited") => true,
		(CommunicationChannelType.WebPush, "WebPushRateLimited") => true,
		_ => false
	};

	private static TimeSpan Backoff(NotificationDispatch dispatch, TimeSpan? requested)
	{
		var jitter = (BinaryPrimitives.ReadUInt16LittleEndian(dispatch.Id.ToByteArray()) + dispatch.AttemptNumber * 17) % 16;
		var delay = TimeSpan.FromMinutes(RetryMinutes[Math.Min(dispatch.AttemptNumber, RetryMinutes.Length) - 1]) + TimeSpan.FromSeconds(jitter);
		return requested > delay ? requested.Value : delay;
	}
}
