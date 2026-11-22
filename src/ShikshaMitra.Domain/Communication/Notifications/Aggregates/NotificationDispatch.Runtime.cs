using System;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing]
public partial class NotificationDispatch
{
	public const int MaximumAutomaticAttempts = 5;
	public const int MaximumAttempts = MaximumAutomaticAttempts + 1;
	public DateTimeOffset? IoStartedAt { get; private set; }
	public Guid? IoLeaseId { get; private set; }
	public DateTimeOffset? AcceptedAt { get; private set; }

	public void BindPlanningEvidence(Guid campaignId, Guid recipientSnapshotId, Guid? providerSettingId)
	{
		NotificationRuntimeGuard.State(Status == NotificationDispatchState.Pending && AttemptNumber == 0 &&
			!CampaignId.HasValue && !RecipientSnapshotId.HasValue && campaignId != Guid.Empty &&
			recipientSnapshotId != Guid.Empty && providerSettingId != Guid.Empty);
		CampaignId = campaignId;
		RecipientSnapshotId = recipientSnapshotId;
		ProviderSettingId = providerSettingId;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}

	public void BeginIo(Guid leaseToken, DateTimeOffset now, Guid? deliveryAttemptId = null)
	{
		RequireActiveLease(leaseToken, now);
		NotificationRuntimeGuard.State(!IoStartedAt.HasValue && !AcceptedAt.HasValue && (!ExpiresAt.HasValue || ExpiresAt > now) &&
			deliveryAttemptId != Guid.Empty);
		IoStartedAt = now.ToUniversalTime(); IoLeaseId = leaseToken;
		DeliveryAttemptId = deliveryAttemptId;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public void RecordOutcome(Guid leaseToken, DateTimeOffset now, NotificationDispatchState outcome, string? providerMessageId = null, string? errorCode = null)
	{
		NotificationRuntimeGuard.Lease(leaseToken != Guid.Empty && IoLeaseId == leaseToken && IoStartedAt.HasValue);
		NotificationRuntimeGuard.State(Status is NotificationDispatchState.Processing or NotificationDispatchState.UnknownAcceptance or
			NotificationDispatchState.ProviderAccepted or NotificationDispatchState.Delivered or NotificationDispatchState.Read);
		NotificationRuntimeGuard.State(outcome is NotificationDispatchState.ProviderAccepted or NotificationDispatchState.Delivered or
			NotificationDispatchState.Read or NotificationDispatchState.Failed or NotificationDispatchState.UnknownAcceptance);
		NotificationRuntimeGuard.State(providerMessageId == null || providerMessageId.Length is > 0 and <= 256);
		NotificationRuntimeGuard.State(ProviderMessageId == null || providerMessageId == null || ProviderMessageId == providerMessageId);
		NotificationRuntimeGuard.ErrorCode(errorCode);
		ProviderMessageId ??= providerMessageId;
		if (Status == NotificationDispatchState.Read || Status == NotificationDispatchState.Delivered && outcome != NotificationDispatchState.Read ||
			Status == NotificationDispatchState.ProviderAccepted && outcome == NotificationDispatchState.UnknownAcceptance)
		{
			ClearLease(); return;
		}
		Status = outcome; ErrorCode = errorCode;
		if (outcome is NotificationDispatchState.ProviderAccepted or NotificationDispatchState.Delivered or NotificationDispatchState.Read)
			AcceptedAt ??= now.ToUniversalTime();
		ClearLease();
	}
	public void ScheduleRetry(Guid leaseToken, DateTimeOffset now, DateTimeOffset nextAttemptAt, string errorCode)
	{
		NotificationRuntimeGuard.Lease(leaseToken != Guid.Empty && IoLeaseId == leaseToken && IoStartedAt.HasValue);
		NotificationRuntimeGuard.State(Status == NotificationDispatchState.Failed && ProviderMessageId == null && !AcceptedAt.HasValue &&
			nextAttemptAt > now && (!ExpiresAt.HasValue || nextAttemptAt < ExpiresAt));
		NotificationRuntimeGuard.ErrorCode(errorCode);
		Status = NotificationDispatchState.Pending; NextAttemptAt = nextAttemptAt.ToUniversalTime(); ErrorCode = errorCode;
		IoStartedAt = null; IoLeaseId = null; ClearLease();
	}

	public bool CanRetryDefiniteRejection => Status == NotificationDispatchState.Failed && !LeaseId.HasValue &&
		IoStartedAt.HasValue && IoLeaseId.HasValue && DeliveryAttemptId.HasValue && !AcceptedAt.HasValue &&
		ProviderMessageId == null && AttemptNumber is > 0 and < MaximumAttempts;

	public void PreserveRetryWindow(DateTimeOffset nextAttemptAt)
	{
		NotificationRuntimeGuard.State(CanRetryDefiniteRejection && nextAttemptAt > IoStartedAt);
		NextAttemptAt = nextAttemptAt.ToUniversalTime();
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}

	public void RetryDefiniteRejection(DateTimeOffset now, DateTimeOffset nextAttemptAt)
	{
		NotificationRuntimeGuard.State(CanRetryDefiniteRejection && nextAttemptAt >= now &&
			nextAttemptAt >= NextAttemptAt && (!ExpiresAt.HasValue || nextAttemptAt < ExpiresAt));
		Status = NotificationDispatchState.Pending;
		NextAttemptAt = nextAttemptAt.ToUniversalTime();
		ErrorCode = NotificationErrorCodes.StaffRetryRequested;
		IoStartedAt = null;
		IoLeaseId = null;
		ClearLease();
	}

	public bool CancelPending()
	{
		if (Status != NotificationDispatchState.Pending || LeaseId.HasValue || IoStartedAt.HasValue ||
			IoLeaseId.HasValue || AcceptedAt.HasValue || ProviderMessageId != null) return false;
		Status = NotificationDispatchState.Skipped;
		ErrorCode = NotificationErrorCodes.CancelledBeforeDelivery;
		ClearLease();
		return true;
	}

	public bool ObserveWhatsAppReceipt(Guid providerSettingId, string providerMessageId, NotificationDispatchState outcome, DateTimeOffset observedAt)
	{
		NotificationRuntimeGuard.State(Channel == CommunicationChannelType.WhatsApp && providerSettingId != Guid.Empty &&
			ProviderSettingId == providerSettingId && !string.IsNullOrEmpty(providerMessageId) && ProviderMessageId == providerMessageId &&
			IoStartedAt.HasValue && IoLeaseId.HasValue && DeliveryAttemptId.HasValue);
		NotificationRuntimeGuard.State(outcome is NotificationDispatchState.ProviderAccepted or NotificationDispatchState.Delivered or
			NotificationDispatchState.Read or NotificationDispatchState.Failed);
		var advances = Status switch
		{
			NotificationDispatchState.UnknownAcceptance => true,
			NotificationDispatchState.ProviderAccepted => outcome != NotificationDispatchState.ProviderAccepted,
			NotificationDispatchState.Failed => outcome is NotificationDispatchState.Delivered or NotificationDispatchState.Read,
			NotificationDispatchState.Delivered => outcome == NotificationDispatchState.Read,
			_ => false
		};
		if (!advances) return false;

		Status = outcome;
		ErrorCode = outcome == NotificationDispatchState.Failed ? "WhatsAppDeliveryFailed" : null;
		if (outcome != NotificationDispatchState.Failed)
			AcceptedAt ??= observedAt > IoStartedAt ? observedAt.ToUniversalTime() : IoStartedAt;
		return true;
	}

	public void SkipBeforeIo(Guid leaseToken, DateTimeOffset now, string errorCode)
	{
		RequireActiveLease(leaseToken, now); NotificationRuntimeGuard.State(!IoStartedAt.HasValue);
		NotificationRuntimeGuard.ErrorCode(errorCode); Status = NotificationDispatchState.Skipped; ErrorCode = errorCode;
		ReleaseUnusedAttempt();
	}
	public void DeferBeforeIo(Guid leaseToken, DateTimeOffset now, DateTimeOffset nextAttemptAt, string reason = "QuietHours")
	{
		RequireActiveLease(leaseToken, now);
		NotificationRuntimeGuard.State(!IoStartedAt.HasValue && nextAttemptAt > now && (!ExpiresAt.HasValue || nextAttemptAt < ExpiresAt));
		NotificationRuntimeGuard.ErrorCode(reason);
		Status = NotificationDispatchState.Pending;
		NextAttemptAt = nextAttemptAt.ToUniversalTime();
		ErrorCode = reason;
		ReleaseUnusedAttempt();
	}
	public bool RecoverExpiredLease(DateTimeOffset now)
	{
		if (Status != NotificationDispatchState.Processing || !LeaseExpiresAt.HasValue || LeaseExpiresAt > now) return false;
		if (IoStartedAt.HasValue) { Status = NotificationDispatchState.UnknownAcceptance; ErrorCode = NotificationErrorCodes.UnknownAcceptance; }
		else if (ExpiresAt <= now) { Status = NotificationDispatchState.Skipped; ErrorCode = "SourceExpired"; }
		else { Status = NotificationDispatchState.Pending; NextAttemptAt = now.ToUniversalTime(); ErrorCode = "LeaseExpiredBeforeIo"; }
		if (IoStartedAt.HasValue) ClearLease();
		else ReleaseUnusedAttempt();
		return true;
	}
	public bool ExpirePending(DateTimeOffset now)
	{
		if (Status != NotificationDispatchState.Pending || LeaseId.HasValue || IoStartedAt.HasValue ||
			!ExpiresAt.HasValue || ExpiresAt > now) return false;
		Status = NotificationDispatchState.Skipped;
		ErrorCode = "SourceExpired";
		ClearLease();
		return true;
	}
	private void ReleaseUnusedAttempt()
	{
		if (AttemptNumber > 0) AttemptNumber--;
		ClearLease();
	}
	private void RequireActiveLease(Guid leaseToken, DateTimeOffset now) => NotificationRuntimeGuard.Lease(
		leaseToken != Guid.Empty && Status == NotificationDispatchState.Processing && LeaseId == leaseToken && LeaseExpiresAt > now);
	private void ClearLease()
	{
		LeaseId = null; ClaimedAt = null; LeaseExpiresAt = null;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
}
