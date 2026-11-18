using System;
using Volo.Abp;
using Volo.Abp.Auditing;

namespace ShikshaMitra.Communication.Notifications;

[DisableAuditing]
public partial class NotificationIntent
{
	public string RuleConfigurationJson { get; private set; } = "{}";
	public NotificationIntentOrigin Origin { get; private set; }
	public string? ErrorCode { get; private set; }
	public DateTimeOffset? PlanningCompletedAt { get; private set; }
	public void BindAutomaticOperation(Guid operationId)
	{
		NotificationRuntimeGuard.State(operationId != Guid.Empty && !CampaignId.HasValue && Origin == NotificationIntentOrigin.Automatic &&
			Status is NotificationIntentState.Ready or NotificationIntentState.Scheduled);
		CampaignId = operationId; ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public void StopBeforePlanning(NotificationIntentState outcome, string errorCode)
	{
		NotificationRuntimeGuard.State(Status is NotificationIntentState.Ready or NotificationIntentState.Scheduled && !LeaseId.HasValue &&
			outcome is NotificationIntentState.NeedsReview or NotificationIntentState.Expired);
		NotificationRuntimeGuard.ErrorCode(errorCode); Status = outcome; ErrorCode = errorCode;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public void BindRuntimeSnapshot(string configurationJson, NotificationIntentOrigin origin, Guid? manualCampaignId = null)
	{
		NotificationRuntimeGuard.State(RuleConfigurationJson == "{}" && !CampaignId.HasValue && Status is NotificationIntentState.Ready or NotificationIntentState.Scheduled);
		NotificationRuntimeGuard.Snapshot(configurationJson);
		NotificationRuntimeGuard.State(Enum.IsDefined(origin) && (origin == NotificationIntentOrigin.ReviewedManual
			? manualCampaignId.HasValue && manualCampaignId != Guid.Empty && ActorUserId.HasValue && ActorUserId != Guid.Empty
			: !manualCampaignId.HasValue));
		RuleConfigurationJson = configurationJson; Origin = origin;
		if (manualCampaignId.HasValue) CampaignId = manualCampaignId;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public void CompletePlanning(Guid leaseToken, DateTimeOffset now)
	{
		NotificationRuntimeGuard.Lease(leaseToken != Guid.Empty && Status == NotificationIntentState.Processing && LeaseId == leaseToken && LeaseExpiresAt > now);
		NotificationRuntimeGuard.Snapshot(RuleConfigurationJson);
		PlanningCompletedAt = now.ToUniversalTime(); Status = NotificationIntentState.AwaitingDelivery;
		LeaseId = null; ClaimedAt = null; LeaseExpiresAt = null;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public void CompleteDelivery(NotificationIntentState outcome, string? errorCode = null)
	{
		NotificationRuntimeGuard.State(PlanningCompletedAt.HasValue && Status is NotificationIntentState.AwaitingDelivery or
			NotificationIntentState.Completed or NotificationIntentState.PartiallyFailed or NotificationIntentState.NeedsReview);
		NotificationRuntimeGuard.State(outcome is NotificationIntentState.Completed or NotificationIntentState.PartiallyFailed or NotificationIntentState.NeedsReview);
		NotificationRuntimeGuard.ErrorCode(errorCode); Status = outcome; ErrorCode = errorCode;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public bool RecoverExpiredLease(DateTimeOffset now)
	{
		if (Status != NotificationIntentState.Processing || !LeaseExpiresAt.HasValue || LeaseExpiresAt.Value > now) return false;
		Status = NotificationIntentState.Ready; ErrorCode = null;
		if (ExpiresAt <= now) { Status = NotificationIntentState.Expired; ErrorCode = "SourceExpired"; }
		else
		{
			try { NotificationRuntimeGuard.Snapshot(RuleConfigurationJson); }
			catch (BusinessException error) when (error.Code == NotificationErrorCodes.RuntimeSnapshotInvalid)
			{
				Status = NotificationIntentState.NeedsReview; ErrorCode = NotificationErrorCodes.RuntimeSnapshotInvalid;
			}
		}
		LeaseId = null; ClaimedAt = null; LeaseExpiresAt = null; ConcurrencyStamp = Guid.NewGuid().ToString("N"); return true;
	}

	public bool ExpirePending(DateTimeOffset now)
	{
		if (Status is not (NotificationIntentState.Ready or NotificationIntentState.Scheduled) ||
			LeaseId.HasValue || !ExpiresAt.HasValue || ExpiresAt > now) return false;
		StopBeforePlanning(NotificationIntentState.Expired, "SourceExpired");
		return true;
	}

	public void ReopenDeliveryProjection()
	{
		NotificationRuntimeGuard.State(PlanningCompletedAt.HasValue && Status is
			NotificationIntentState.Completed or NotificationIntentState.PartiallyFailed or NotificationIntentState.NeedsReview);
		Status = NotificationIntentState.AwaitingDelivery;
		ErrorCode = null;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
	public void StopProcessing(Guid leaseToken, DateTimeOffset now, NotificationIntentState outcome, string errorCode)
	{
		NotificationRuntimeGuard.Lease(leaseToken != Guid.Empty && Status == NotificationIntentState.Processing && LeaseId == leaseToken && LeaseExpiresAt > now);
		NotificationRuntimeGuard.State(outcome is NotificationIntentState.Cancelled or NotificationIntentState.Expired or NotificationIntentState.NeedsReview);
		NotificationRuntimeGuard.ErrorCode(errorCode);
		Status = outcome; ErrorCode = errorCode; LeaseId = null; ClaimedAt = null; LeaseExpiresAt = null;
		ConcurrencyStamp = Guid.NewGuid().ToString("N");
	}
}
