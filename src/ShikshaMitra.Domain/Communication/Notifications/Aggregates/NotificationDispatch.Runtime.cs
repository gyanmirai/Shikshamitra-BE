using System;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing]
public partial class NotificationDispatch
{
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

	public void BeginIo(Guid leaseToken, DateTimeOffset now)
	{
		RequireActiveLease(leaseToken, now); NotificationRuntimeGuard.State(!IoStartedAt.HasValue && !AcceptedAt.HasValue && (!ExpiresAt.HasValue || ExpiresAt > now));
		IoStartedAt = now.ToUniversalTime(); IoLeaseId = leaseToken;
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
	public void SkipBeforeIo(Guid leaseToken, DateTimeOffset now, string errorCode)
	{
		RequireActiveLease(leaseToken, now); NotificationRuntimeGuard.State(!IoStartedAt.HasValue);
		NotificationRuntimeGuard.ErrorCode(errorCode); Status = NotificationDispatchState.Skipped; ErrorCode = errorCode;
		ReleaseUnusedAttempt();
	}
	public void DeferBeforeIo(Guid leaseToken, DateTimeOffset now, DateTimeOffset nextAttemptAt)
	{
		RequireActiveLease(leaseToken, now);
		NotificationRuntimeGuard.State(!IoStartedAt.HasValue && nextAttemptAt > now && (!ExpiresAt.HasValue || nextAttemptAt < ExpiresAt));
		Status = NotificationDispatchState.Pending;
		NextAttemptAt = nextAttemptAt.ToUniversalTime();
		ErrorCode = "QuietHours";
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
