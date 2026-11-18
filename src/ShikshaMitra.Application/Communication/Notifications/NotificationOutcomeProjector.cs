using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class NotificationOutcomeProjector(ICurrentTenant tenant, IUnitOfWorkManager units, NotificationOutcomeLock outcomeLock,
	IRepository<NotificationIntent, Guid> intents, IRepository<NotificationDispatch, Guid> dispatches,
	IRepository<CommunicationCampaign, Guid> campaigns, IAsyncQueryableExecuter queries) : ITransientDependency
{
	private static readonly NotificationIntentState[] ActiveIntents =
		[NotificationIntentState.Scheduled, NotificationIntentState.Ready, NotificationIntentState.Processing, NotificationIntentState.AwaitingDelivery];
	private static readonly NotificationDispatchState[] ActiveDispatches = [NotificationDispatchState.Pending, NotificationDispatchState.Processing];
	private static readonly CommunicationCampaignStatus[] ProjectableOperations = [
		CommunicationCampaignStatus.Scheduled, CommunicationCampaignStatus.Queued, CommunicationCampaignStatus.Sending,
		CommunicationCampaignStatus.Sent, CommunicationCampaignStatus.PartiallyFailed, CommunicationCampaignStatus.Failed
	];
	private sealed record DispatchTally(Guid IntentId, NotificationDispatchState State, string? ErrorCode, int Count);

	public async Task<int> RefreshBatchAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
		var ready = await FindOperationsAsync(tenantId, batchSize, cancellationToken);
		var count = 0;
		foreach (var id in ready)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (await RefreshAsync(tenantId, id, cancellationToken)) count++;
		}
		return count;
	}

	internal static async Task ReopenIntentAsync(IRepository<NotificationIntent, Guid> intents, IAsyncQueryableExecuter queries,
		Guid tenantId, NotificationDispatch dispatch, CancellationToken cancellationToken)
	{
		var intent = await queries.FirstOrDefaultAsync((await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId &&
			!x.IsDeleted && x.Id == dispatch.IntentId && x.CampaignId == dispatch.CampaignId && x.PlanningCompletedAt != null &&
			(x.Status == NotificationIntentState.NeedsReview || x.Status == NotificationIntentState.Completed ||
				x.Status == NotificationIntentState.PartiallyFailed)), cancellationToken);
		if (intent == null) return;
		intent.ReopenDeliveryProjection();
		await intents.UpdateAsync(intent, cancellationToken: cancellationToken);
	}

	private async Task<IReadOnlyList<Guid>> FindOperationsAsync(Guid tenantId, int batchSize, CancellationToken cancellationToken)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: false);
		var tenantIntents = await TenantIntentsAsync(tenantId);
		var queued = (await TenantDispatchesAsync(tenantId)).Where(d => Enumerable.Contains(ActiveDispatches, d.Status));
		var settled = tenantIntents.Where(i => i.Status == NotificationIntentState.AwaitingDelivery && i.PlanningCompletedAt != null &&
			!queued.Any(d => d.IntentId == i.Id && d.CampaignId == i.CampaignId));
		var active = tenantIntents.Where(i => Enumerable.Contains(ActiveIntents, i.Status));
		var operations = await OperationsAsync(tenantId);
		var ready = operations.Where(operation => settled.Any(i => i.CampaignId == operation.Id &&
				i.SourceId == operation.SourceEntityId && i.SourceType == operation.SourceEntityType) ||
			(operation.Status == CommunicationCampaignStatus.Queued || operation.Status == CommunicationCampaignStatus.Scheduled ||
				operation.Status == CommunicationCampaignStatus.Sending) &&
			tenantIntents.Any(i => i.CampaignId == operation.Id && i.SourceId == operation.SourceEntityId && i.SourceType == operation.SourceEntityType) &&
			!active.Any(i => i.CampaignId == operation.Id && i.SourceId == operation.SourceEntityId && i.SourceType == operation.SourceEntityType))
			.OrderBy(x => x.Id).Select(x => x.Id);
		var ids = await queries.ToListAsync(ready.Take(batchSize), cancellationToken);
		await unit.CompleteAsync(cancellationToken);
		return ids;
	}

	private async Task<bool> RefreshAsync(Guid tenantId, Guid operationId, CancellationToken cancellationToken)
	{
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		await outcomeLock.AcquireAsync(operationId, cancellationToken);
		var changed = await RefreshLockedAsync(tenantId, operationId, cancellationToken);
		await unit.CompleteAsync(cancellationToken);
		return changed;
	}

	internal async Task<bool> RefreshLockedAsync(Guid tenantId, Guid operationId, CancellationToken cancellationToken)
	{
		var unit = units.Current ?? throw new BusinessException(NotificationErrorCodes.StateConflict);
		var operation = await queries.FirstOrDefaultAsync((await OperationsAsync(tenantId)).Where(x => x.Id == operationId), cancellationToken);
		if (operation == null) return false;
		var owned = (await TenantIntentsAsync(tenantId)).Where(x => x.CampaignId == operationId &&
			x.SourceId == operation.SourceEntityId && x.SourceType == operation.SourceEntityType);
		var awaiting = await queries.ToListAsync(owned.Where(x => x.Status == NotificationIntentState.AwaitingDelivery &&
			x.PlanningCompletedAt != null), cancellationToken);
		var tallies = await queries.ToListAsync((await TenantDispatchesAsync(tenantId)).Where(d => d.CampaignId == operationId &&
				owned.Any(i => i.Id == d.IntentId))
			.GroupBy(x => new { x.IntentId, x.Status, x.ErrorCode })
			.Select(x => new DispatchTally(x.Key.IntentId, x.Key.Status, x.Key.ErrorCode, x.Count())), cancellationToken);
		var byIntent = tallies.ToLookup(x => x.IntentId);
		var changed = false;
		foreach (var intent in awaiting)
		{
			var rows = byIntent[intent.Id];
			if (rows.Any(x => Enumerable.Contains(ActiveDispatches, x.State))) continue;
			var (state, reason) = SummarizeIntent(rows);
			intent.CompleteDelivery(state, reason);
			await intents.UpdateAsync(intent, cancellationToken: cancellationToken);
			changed = true;
		}
		if (changed) await unit.SaveChangesAsync(cancellationToken);
		var states = await queries.ToListAsync(owned.Select(x => x.Status).Distinct(), cancellationToken);
		if (states.Count == 0) return false;
		var pending = tallies.Where(x => Enumerable.Contains(ActiveDispatches, x.State)).Sum(x => x.Count);
		var delivered = tallies.Where(x => x.State is NotificationDispatchState.Delivered or NotificationDispatchState.Read).Sum(x => x.Count);
		var failed = tallies.Where(x => x.State == NotificationDispatchState.Failed).Sum(x => x.Count);
		var status = SummarizeOperation(states, tallies);
		if (operation.Status != status || operation.QueuedCount != pending || operation.DeliveredCount != delivered || operation.FailedCount != failed)
		{
			operation.Status = status;
			operation.QueuedCount = pending;
			operation.DeliveredCount = delivered;
			operation.FailedCount = failed;
			await campaigns.UpdateAsync(operation, cancellationToken: cancellationToken);
			changed = true;
		}
		return changed;
	}

	private async Task<IQueryable<NotificationIntent>> TenantIntentsAsync(Guid tenantId) =>
		(await intents.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);

	private async Task<IQueryable<NotificationDispatch>> TenantDispatchesAsync(Guid tenantId) =>
		(await dispatches.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted);

	private async Task<IQueryable<CommunicationCampaign>> OperationsAsync(Guid tenantId) =>
		(await campaigns.GetQueryableAsync()).Where(x => x.TenantId == tenantId && !x.IsDeleted &&
			x.CampaignType == NotificationCampaignTypes.Academic && Enumerable.Contains(ProjectableOperations, x.Status));

	private static (NotificationIntentState State, string? Reason) SummarizeIntent(IEnumerable<DispatchTally> rows)
	{
		if (rows.Any(x => x.State == NotificationDispatchState.UnknownAcceptance))
			return (NotificationIntentState.NeedsReview, NotificationErrorCodes.UnknownAcceptance);
		if (rows.Any(x => x.State == NotificationDispatchState.Failed))
			return (NotificationIntentState.PartiallyFailed, "DeliveryFailed");
		if (rows.Any(x => x.State == NotificationDispatchState.Skipped && x.ErrorCode is NotificationErrorCodes.ProviderUnavailable or
			"ProviderChanged" or "DispatchPayloadInvalid" or "PreferenceConfigurationInvalid" or "RecipientResolverUnavailable" or
			NotificationErrorCodes.TemplateInvalid or NotificationErrorCodes.RuntimeSnapshotInvalid or NotificationErrorCodes.RuleConfigurationInvalid))
			return (NotificationIntentState.NeedsReview, "DeliveryConfigurationNeedsReview");
		if (!rows.Any(x => IsAccepted(x.State))) return (NotificationIntentState.Completed, "NoEligibleRecipients");
		return (NotificationIntentState.Completed, rows.Any(x => x.State == NotificationDispatchState.Skipped) ? "SomeRecipientsSkipped" : null);
	}

	private static CommunicationCampaignStatus SummarizeOperation(IReadOnlyList<NotificationIntentState> states,
		IReadOnlyList<DispatchTally> tallies)
	{
		if (states.Any(x => Enumerable.Contains(ActiveIntents, x)))
			return states.All(x => x == NotificationIntentState.Scheduled) ? CommunicationCampaignStatus.Scheduled : CommunicationCampaignStatus.Queued;
		var accepted = tallies.Any(x => IsAccepted(x.State));
		var review = states.Any(x => x is NotificationIntentState.NeedsReview or NotificationIntentState.PartiallyFailed);
		return (accepted, review) switch {
			(true, true) => CommunicationCampaignStatus.PartiallyFailed,
			(true, false) => CommunicationCampaignStatus.Sent,
			(false, true) => CommunicationCampaignStatus.Failed,
			_ => CommunicationCampaignStatus.Cancelled
		};
	}

	private static bool IsAccepted(NotificationDispatchState state) =>
		state is NotificationDispatchState.ProviderAccepted or NotificationDispatchState.Delivered or NotificationDispatchState.Read;
}
