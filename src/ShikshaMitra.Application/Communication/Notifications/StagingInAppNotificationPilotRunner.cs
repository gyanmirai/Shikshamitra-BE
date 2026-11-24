using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class StagingInAppNotificationPilotRunner(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	IConfiguration configuration, IHostEnvironment environment, INotificationQueueRepository queue, NotificationDispatchPlanner planner,
	NotificationOutcomeLock outcomeLock, NotificationDispatchHeartbeat heartbeat, INotificationDispatcher dispatcher,
	NotificationOutcomeProjector projector, NotificationDeliveryProjection delivery, IRepository<CommunicationCampaign, Guid> campaigns,
	IRepository<NotificationIntent, Guid> intents, IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<CommunicationRecipientSnapshot, Guid> evidence, IRepository<IdentityUser, Guid> users, IDataFilter filters,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	private sealed record IntentScope(Guid Id, Guid SourceId, string SourceType, bool IsDeleted);

	public async Task RunAsync(StagingInAppNotificationPilot pilot, CancellationToken cancellationToken)
	{
		if (tenant.Id != pilot.TenantId || pilot.TenantId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (NotificationQueueWorkerConfiguration.ReadStagingInAppPilot(configuration, environment.IsStaging()) != pilot ||
			NotificationQueueWorkerConfiguration.IsSchoolPaused(configuration, pilot.TenantId) ||
			NotificationQueueWorkerConfiguration.ReadPausedChannels(configuration, pilot.TenantId).Contains(CommunicationChannelType.InApp))
			return;

		var claim = await ClaimOrProjectAsync(pilot, cancellationToken, claimWork: true);
		if (claim == null) return;
		await heartbeat.RunAsync(claim, dispatcher, cancellationToken, pilot);
		await ClaimOrProjectAsync(pilot, cancellationToken, claimWork: false);
	}

	private async Task<NotificationQueueClaim?> ClaimOrProjectAsync(StagingInAppNotificationPilot pilot,
		CancellationToken cancellationToken, bool claimWork)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.ReadCommitted);
		await outcomeLock.AcquireAsync(pilot.OperationId, cancellationToken);
		if (!await HasDemoRecipientAsync(pilot, cancellationToken)) return null;
		var sourceType = await ReadRegularOperationSourceTypeAsync(pilot, cancellationToken);
		if (sourceType == null) return null;

		var selected = await ReadIntentAsync(pilot, cancellationToken);
		if (selected == null || selected.SourceType != sourceType) return null;
		var rows = await ReadDispatchesAsync(pilot, cancellationToken);
		if (rows.Length == 0)
		{
			if (!claimWork || await queries.AnyAsync((await evidence.GetQueryableAsync()).Where(x => x.TenantId == pilot.TenantId &&
				x.CampaignId == pilot.OperationId), cancellationToken)) return null;
			var intent = (await queue.ClaimIntentsAsync(Now(), 1, NotificationDispatchHeartbeat.LeaseDuration,
				cancellationToken, pilot)).SingleOrDefault();
			if (intent == null || intent.Id != selected.Id) return null;
			await planner.PlanAsync(intent, cancellationToken);
			rows = await ReadDispatchesAsync(pilot, cancellationToken);
		}

		// A scope mismatch rolls back the intent claim and any rows created by the planner.
		if (rows is not [var row] || row.IsDeleted || row.IntentId != selected.Id || row.Channel != CommunicationChannelType.InApp ||
			row.ProviderSettingId.HasValue || row.AttemptNumber > 1 ||
			(row.Status == NotificationDispatchState.Pending && row.AttemptNumber != 0) || !MatchesRecipient(row, pilot) ||
			!await HasSingleRecipientSnapshotAsync(pilot, row, cancellationToken))
			return null;

		var now = Now();
		if ((row.AttemptNumber == 1 && row.RecoverExpiredLease(now)) || (row.AttemptNumber == 0 && row.ExpirePending(now)))
		{
			if (row.Status == NotificationDispatchState.UnknownAcceptance) await delivery.RecordAsync(row, now, cancellationToken);
			await dispatches.UpdateAsync(row, autoSave: true, cancellationToken: cancellationToken);
		}

		NotificationQueueClaim? claim = null;
		if (claimWork && row.Status == NotificationDispatchState.Pending && row.AttemptNumber == 0)
			claim = (await queue.ClaimDispatchesAsync(now, 1, NotificationDispatchHeartbeat.LeaseDuration,
				cancellationToken, pilot: pilot)).SingleOrDefault();
		else if (row.Status is not (NotificationDispatchState.Pending or NotificationDispatchState.Processing))
			await projector.RefreshLockedAsync(pilot.TenantId, pilot.OperationId, cancellationToken);
		await unit.CompleteAsync(cancellationToken);
		return claim;
	}

	private async Task<bool> HasDemoRecipientAsync(StagingInAppNotificationPilot pilot, CancellationToken cancellationToken)
	{
		var now = Now();
		return await queries.AnyAsync((await users.GetQueryableAsync()).Where(x => x.TenantId == pilot.TenantId &&
			x.Id == pilot.RecipientUserId && !x.IsDeleted && x.IsActive && (!x.LockoutEnabled || x.LockoutEnd <= now || x.LockoutEnd == null) &&
			(x.NormalizedUserName == "STUDENT-DEMO" || x.NormalizedUserName == "PARENT-DEMO" ||
				x.NormalizedUserName == "TEACHER-DEMO-1" || x.NormalizedUserName == "TEACHER-DEMO-2")), cancellationToken);
	}

	private async Task<string?> ReadRegularOperationSourceTypeAsync(StagingInAppNotificationPilot pilot, CancellationToken cancellationToken) =>
		await queries.FirstOrDefaultAsync((await campaigns.GetQueryableAsync()).Where(x => x.Id == pilot.OperationId &&
			x.TenantId == pilot.TenantId && !x.IsDeleted && x.CampaignType == NotificationCampaignTypes.Academic &&
			x.SourceEntityId == pilot.SourceId && x.SourceEntityType != null &&
			x.SourceEntityType != NotificationCampaignTypes.AcademicTemplateTestSource &&
			(x.Status == CommunicationCampaignStatus.Queued || x.Status == CommunicationCampaignStatus.Scheduled ||
				x.Status == CommunicationCampaignStatus.Sending) &&
			(!x.ApprovalRequired || x.ApprovedByUserId.HasValue && x.ApprovedAt.HasValue))
			.Select(x => x.SourceEntityType), cancellationToken);

	private async Task<IntentScope?> ReadIntentAsync(StagingInAppNotificationPilot pilot, CancellationToken cancellationToken)
	{
		using (filters.Disable<ISoftDelete>())
		{
			var rows = await queries.ToListAsync((await intents.GetQueryableAsync())
				.Where(x => x.TenantId == pilot.TenantId && x.CampaignId == pilot.OperationId)
				.Select(x => new IntentScope(x.Id, x.SourceId, x.SourceType, x.IsDeleted))
				.Take(2), cancellationToken);
			return rows is [var row] && !row.IsDeleted && row.SourceId == pilot.SourceId ? row : null;
		}
	}

	private async Task<bool> HasSingleRecipientSnapshotAsync(StagingInAppNotificationPilot pilot, NotificationDispatch row,
		CancellationToken cancellationToken)
	{
		using (filters.Disable<ISoftDelete>())
		{
			var recipients = await queries.ToListAsync((await evidence.GetQueryableAsync())
				.Where(x => x.TenantId == pilot.TenantId && x.CampaignId == pilot.OperationId)
				.Select(x => new { x.Id, x.UserId, x.IsEligible, x.IsDeleted }).Take(2), cancellationToken);
			return recipients is [var item] && item.Id == row.RecipientSnapshotId && item.UserId == pilot.RecipientUserId &&
				item.IsEligible && !item.IsDeleted;
		}
	}

	private async Task<NotificationDispatch[]> ReadDispatchesAsync(StagingInAppNotificationPilot pilot, CancellationToken cancellationToken)
	{
		using (filters.Disable<ISoftDelete>())
			return (await queries.ToListAsync((await dispatches.GetQueryableAsync())
				.Where(x => x.TenantId == pilot.TenantId && x.CampaignId == pilot.OperationId).Take(2), cancellationToken)).ToArray();
	}

	private static bool MatchesRecipient(NotificationDispatch row, StagingInAppNotificationPilot pilot) =>
		NotificationRuntimeJson.Deserialize<NotificationRecipientRefDto>(row.RecipientReferenceJson,
			NotificationErrorCodes.RuntimeSnapshotInvalid).UserId == pilot.RecipientUserId;

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
