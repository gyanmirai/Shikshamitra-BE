using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false), DisableAuditing]
public class AcademicNotificationDeliveryActionAppService(AcademicNotificationAccessService access,
	AcademicNotificationOperationAccess operations, NotificationOutcomeLock outcomeLock, NotificationOutcomeProjector outcomes,
	NotificationDispatchReadiness readiness, AcademicNotificationStaffAudit audit,
	IRepository<NotificationDispatch, Guid> dispatches, IRepository<NotificationIntent, Guid> intents,
	IRepository<DeliveryAttempt, Guid> attempts, IRepository<CommunicationCampaign, Guid> campaigns, IClock clock)
	: ApplicationService, IAcademicNotificationDeliveryActionAppService
{
	[UnitOfWork(true)]
	public virtual async Task<AcademicNotificationRetryDto> RetryAsync(Guid dispatchId, CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, callerScope) = await access.RequireStaffActionAsync(cancellationToken);
		var dispatchQuery = (await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.Id == dispatchId &&
			!x.IsDeleted && x.CampaignId.HasValue);
		var reference = await AsyncExecuter.SingleOrDefaultAsync(dispatchQuery.Select(x => new { x.CampaignId, x.IntentId }), cancellationToken)
			?? throw new EntityNotFoundException(typeof(NotificationDispatch), dispatchId);
		var operationId = reference.CampaignId!.Value;
		await outcomeLock.AcquireStaffActionAsync(operationId, cancellationToken);
		var authorized = await operations.RequireAsync(operationId, reference.IntentId, tenantId, callerScope, cancellationToken);
		var dispatch = await AsyncExecuter.SingleOrDefaultAsync(dispatchQuery.Where(x =>
			x.IntentId == reference.IntentId && x.CampaignId == operationId), cancellationToken)
			?? throw new EntityNotFoundException(typeof(NotificationDispatch), dispatchId);
		if (authorized.Record.Status is CommunicationCampaignStatus.Cancelled or CommunicationCampaignStatus.PendingApproval)
			throw RequiresReview();
		if (dispatch.Status == NotificationDispatchState.Pending && dispatch.ErrorCode == NotificationErrorCodes.StaffRetryRequested)
			return Map(dispatch, operationId);
		if (!dispatch.CanRetryDefiniteRejection) throw RequiresReview();

		var last = await AsyncExecuter.SingleOrDefaultAsync((await attempts.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == dispatch.DeliveryAttemptId && !x.IsDeleted && x.CampaignId == operationId && x.NotificationDispatchId == dispatch.Id &&
			x.Channel == dispatch.Channel && x.RecipientSnapshotId == dispatch.RecipientSnapshotId && x.AttemptNumber == dispatch.AttemptNumber),
			cancellationToken);
		if (last == null || !NotificationDispatchRetryPolicy.CanRequestStaffRetry(dispatch, last)) throw RequiresReview();
		var intent = await AsyncExecuter.SingleAsync((await intents.GetQueryableAsync()).Where(x =>
			x.Id == reference.IntentId && x.TenantId == tenantId && !x.IsDeleted && x.CampaignId == operationId), cancellationToken);
		if (!intent.PlanningCompletedAt.HasValue || intent.Status is not (NotificationIntentState.AwaitingDelivery or NotificationIntentState.Completed or
			NotificationIntentState.PartiallyFailed or NotificationIntentState.NeedsReview)) throw RequiresReview();

		var now = Now();
		var proposed = dispatch.NextAttemptAt > now ? dispatch.NextAttemptAt : now;
		var decision = await readiness.CheckAsync(intent, dispatch, proposed, cancellationToken);
		if (decision.ExclusionCode != null || !decision.DueAt.HasValue ||
			decision.ExpiresAt <= decision.DueAt || dispatch.ExpiresAt <= decision.DueAt)
			throw RequiresReview();
		var reason = last.ErrorCode!;
		dispatch.RetryDefiniteRejection(now, decision.DueAt.Value);
		await dispatches.UpdateAsync(dispatch, cancellationToken: cancellationToken);
		await NotificationOutcomeProjector.ReopenIntentAsync(intents, AsyncExecuter, tenantId, dispatch, cancellationToken);
		await CurrentUnitOfWork!.SaveChangesAsync(cancellationToken);
		await outcomes.RefreshLockedAsync(tenantId, operationId, cancellationToken);
		await audit.WriteAsync(tenantId, actorId, "RetryDispatch", operationId, dispatch.Id, authorized.Definition.EventKey, reason,
			null, null, cancellationToken);
		return Map(dispatch, operationId);
	}

	[UnitOfWork(true)]
	public virtual async Task<AcademicNotificationCancellationDto> CancelAsync(Guid operationId, CancellationToken cancellationToken = default)
	{
		var (tenantId, actorId, callerScope) = await access.RequireStaffActionAsync(cancellationToken);
		await outcomeLock.AcquireStaffActionAsync(operationId, cancellationToken);
		var authorized = await operations.RequireAsync(operationId, null, tenantId, callerScope, cancellationToken);
		var op = await AsyncExecuter.SingleAsync((await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			x.Id == operationId && !x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic), cancellationToken);

		var intentIds = authorized.Intents.Select(x => x.Id).ToArray();
		var ownedIntents = (await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.CampaignId == operationId && Enumerable.Contains(intentIds, x.Id));
		var ownedDispatches = (await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.CampaignId == operationId && Enumerable.Contains(intentIds, x.IntentId));
		var unplanned = await AsyncExecuter.ToListAsync(ownedIntents.Where(x =>
			x.Status == NotificationIntentState.Ready || x.Status == NotificationIntentState.Scheduled), cancellationToken);
		var pending = await AsyncExecuter.ToListAsync(ownedDispatches.Where(x => x.Status == NotificationDispatchState.Pending), cancellationToken);

		var cancelledIntents = unplanned.Where(x => x.CancelPending()).ToArray();
		var cancelledDispatches = pending.Where(x => x.CancelPending()).ToArray();
		if (cancelledIntents.Length > 0) await intents.UpdateManyAsync(cancelledIntents, cancellationToken: cancellationToken);
		if (cancelledDispatches.Length > 0) await dispatches.UpdateManyAsync(cancelledDispatches, cancellationToken: cancellationToken);
		var pendingApproval = op.Status == CommunicationCampaignStatus.PendingApproval;
		var stopFutureReminders = op.RejectionReason == null && AcademicNotificationEventKeys.IsLearningTaskDueReminder(authorized.Definition.EventKey) &&
			await AsyncExecuter.AnyAsync(ownedIntents.Where(x => x.Origin == NotificationIntentOrigin.Automatic), cancellationToken);
		if (pendingApproval) op.Status = CommunicationCampaignStatus.Cancelled;
		if (pendingApproval || stopFutureReminders)
		{
			op.RejectionReason = NotificationErrorCodes.CancelledBeforeDelivery;
			await campaigns.UpdateAsync(op, cancellationToken: cancellationToken);
		}
		if (pendingApproval || stopFutureReminders || cancelledIntents.Length != 0 || cancelledDispatches.Length != 0)
		{
			await CurrentUnitOfWork!.SaveChangesAsync(cancellationToken);
			if (!pendingApproval) await outcomes.RefreshLockedAsync(tenantId, operationId, cancellationToken);
			await audit.WriteAsync(tenantId, actorId, "CancelOperation", operationId, null, authorized.Definition.EventKey,
				NotificationErrorCodes.CancelledBeforeDelivery, cancelledIntents.Length, cancelledDispatches.Length, cancellationToken);
		}
		var remaining = await AsyncExecuter.CountAsync(ownedIntents.Where(x => x.Status == NotificationIntentState.Processing), cancellationToken)
			+ await AsyncExecuter.CountAsync(ownedDispatches.Where(x => x.Status == NotificationDispatchState.Processing), cancellationToken);
		return new(operationId, op.Status, cancelledIntents.Length, cancelledDispatches.Length, remaining);
	}

	private static AcademicNotificationRetryDto Map(NotificationDispatch dispatch, Guid operationId) =>
		new(dispatch.Id, operationId, dispatch.Status, dispatch.NextAttemptAt);

	private static BusinessException RequiresReview() => new(NotificationErrorCodes.RetryRequiresReview);
	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
