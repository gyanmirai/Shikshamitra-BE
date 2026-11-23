using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
	WhatsAppProviderEventProcessor whatsappReceipts,
	ILogger<NotificationTenantQueueRunner> logger, IConfiguration configuration) : ITransientDependency
{
	public async Task RunAsync(int batchSize, CancellationToken cancellationToken)
	{
		if (tenant.Id is not Guid tenantId || tenantId == Guid.Empty) throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (batchSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(batchSize));
		if (NotificationQueueWorkerConfiguration.IsPilotConfigured(configuration)) return;
		var recovered = await recovery.RecoverAsync(Now(), batchSize, cancellationToken);
		if (recovered.Intents > 0 || recovered.Dispatches > 0)
			logger.LogInformation("AcademicNotificationQueueRecovery TenantId={TenantId} IntentLeases={IntentLeases} DispatchLeases={DispatchLeases} " +
				"ExpiredIntents={ExpiredIntents} ExpiredDispatches={ExpiredDispatches}", tenantId, recovered.IntentLeases, recovered.DispatchLeases,
				recovered.Intents - recovered.IntentLeases, recovered.Dispatches - recovered.DispatchLeases);
		await ProcessAsync(tenantId, batchSize, dispatch: false, cancellationToken);
		await ProcessAsync(tenantId, batchSize, dispatch: true, cancellationToken);
		await whatsappReceipts.ProcessBatchAsync(batchSize, cancellationToken);
		await projector.RefreshBatchAsync(batchSize, cancellationToken);
	}

	public async Task RunTemplateTestPilotAsync(NotificationTemplateTestPilot pilot, CancellationToken cancellationToken)
	{
		if (tenant.Id != pilot.TenantId || pilot.TenantId == Guid.Empty)
			throw new BusinessException(NotificationErrorCodes.TenantRequired);
		if (units.Current != null) throw new BusinessException(NotificationErrorCodes.StateConflict);
		if (NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(configuration) != pilot ||
			NotificationQueueWorkerConfiguration.IsSchoolPaused(configuration, pilot.TenantId) ||
			NotificationQueueWorkerConfiguration.ReadPausedChannels(configuration, pilot.TenantId)
				.Contains(pilot.Channel)) return;
		await recovery.RecoverTemplateTestPilotAsync(pilot, Now(), cancellationToken);
		var claim = await ClaimNextAsync(pilot.TenantId, dispatch: true, cancellationToken, pilot);
		if (claim != null)
		{
			if (claim.TenantId != pilot.TenantId) throw new BusinessException(NotificationErrorCodes.TenantRequired);
			await heartbeat.RunAsync(claim, dispatcher, cancellationToken, pilot);
		}
		await projector.RefreshTemplateTestPilotAsync(pilot, cancellationToken);
	}

	private async Task ProcessAsync(Guid tenantId, int batchSize, bool dispatch, CancellationToken cancellationToken)
	{
		for (var index = 0; index < batchSize; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var claim = await ClaimNextAsync(tenantId, dispatch, cancellationToken);
			if (claim == null) return;
			if (claim.TenantId != tenantId) throw new BusinessException(NotificationErrorCodes.TenantRequired);
			try
			{
				if (dispatch)
					await heartbeat.RunAsync(claim, dispatcher, cancellationToken);
				else
				{
					using var unit = units.Begin(requiresNew: true, isTransactional: true);
					var exclusions = await planner.PlanAsync(claim, cancellationToken);
					await unit.CompleteAsync(cancellationToken);
					foreach (var item in exclusions)
						logger.LogInformation("AcademicNotificationPlanningExclusion TenantId={TenantId} Channel={Channel} Reason={Reason} Count={Count}",
							tenantId, item.Channel, item.Reason, item.Count);
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

	private async Task<NotificationQueueClaim?> ClaimNextAsync(Guid tenantId, bool dispatch, CancellationToken cancellationToken,
		NotificationTemplateTestPilot? pilot = null)
	{
		if (NotificationQueueWorkerConfiguration.IsSchoolPaused(configuration, tenantId)) return null;
		if (pilot == null ? NotificationQueueWorkerConfiguration.IsPilotConfigured(configuration) :
			NotificationQueueWorkerConfiguration.ReadTemplateTestPilot(configuration) != pilot) return null;
		var pausedChannels = dispatch ? NotificationQueueWorkerConfiguration.ReadPausedChannels(configuration, tenantId) : [];
		using var claim = units.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.ReadCommitted);
		IReadOnlyList<NotificationQueueClaim> rows = dispatch
			? await queue.ClaimDispatchesAsync(Now(), 1, NotificationDispatchHeartbeat.LeaseDuration, cancellationToken,
				maxActivePerChannel: 1, pausedChannels: pausedChannels.Length > 0 ? pausedChannels : null, pilot: pilot)
			: await queue.ClaimIntentsAsync(Now(), 1, NotificationDispatchHeartbeat.LeaseDuration, cancellationToken);
		await claim.CompleteAsync(cancellationToken);
		return rows.SingleOrDefault();
	}

	private DateTimeOffset Now() => new(clock.Now.ToUniversalTime());
}
