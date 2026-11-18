using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false), DisableAuditing]
public sealed class NotificationTenantQueueRunner(ICurrentTenant tenant, IUnitOfWorkManager units, IClock clock,
	INotificationQueueRepository queue, NotificationDispatchPlanner planner, INotificationDispatcher dispatcher,
	NotificationDispatchHeartbeat heartbeat, NotificationLeaseRecoveryService recovery, NotificationOutcomeProjector projector,
	ILogger<NotificationTenantQueueRunner> logger) : ITransientDependency
{
	public async Task RunAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
		await recovery.RecoverAsync(Now(), batchSize, cancellationToken);
		await ProcessAsync(tenantId, batchSize, dispatch: false, cancellationToken);
		await ProcessAsync(tenantId, batchSize, dispatch: true, cancellationToken);
		await projector.RefreshBatchAsync(batchSize, cancellationToken);
	}

	private async Task ProcessAsync(Guid tenantId, int batchSize, bool dispatch, CancellationToken cancellationToken)
	{
		for (var index = 0; index < batchSize; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var claim = await ClaimNextAsync(dispatch, cancellationToken);
			if (claim == null) return;
			if (claim.TenantId != tenantId) throw new BusinessException(NotificationErrorCodes.TenantRequired);
			try
			{
				if (dispatch)
					await heartbeat.RunAsync(claim, dispatcher, cancellationToken);
				else
				{
					using var unit = units.Begin(requiresNew: true, isTransactional: true);
					await planner.PlanAsync(claim, cancellationToken);
					await unit.CompleteAsync(cancellationToken);
				}
			}
			catch (BusinessException error) when (error.Code == NotificationErrorCodes.LeaseLost)
			{
			}
			catch (AbpDbConcurrencyException)
			{
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception)
			{
				logger.LogWarning("Notification queue item {QueueId} for school {TenantId} could not finish this turn.", claim.Id, tenantId);
			}
		}
	}

	private async Task<NotificationQueueClaim?> ClaimNextAsync(bool dispatch, CancellationToken cancellationToken)
	{
		using var claim = units.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.ReadCommitted);
		IReadOnlyList<NotificationQueueClaim> rows = dispatch
			? await queue.ClaimDispatchesAsync(Now(), 1, NotificationDispatchHeartbeat.LeaseDuration, cancellationToken, maxActivePerChannel: 1)
			: await queue.ClaimIntentsAsync(Now(), 1, NotificationDispatchHeartbeat.LeaseDuration, cancellationToken);
		await claim.CompleteAsync(cancellationToken);
		return rows.SingleOrDefault();
	}

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
