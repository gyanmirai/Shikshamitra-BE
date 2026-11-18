using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationDispatchHeartbeat(IServiceScopeFactory scopes, ILogger<NotificationDispatchHeartbeat> logger)
	: ITransientDependency
{
	internal static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

	internal async Task RunAsync(NotificationQueueClaim claim, INotificationDispatcher dispatcher, CancellationToken cancellationToken)
	{
		using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var heartbeat = RenewUntilFinishedAsync(claim, stop.Token);
		try
		{
			await dispatcher.DispatchAsync(claim.Id, claim.LeaseId, cancellationToken);
		}
		finally
		{
			stop.Cancel();
			await heartbeat;
		}
	}

	private async Task RenewUntilFinishedAsync(NotificationQueueClaim claim, CancellationToken stop)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
		try
		{
			while (await timer.WaitForNextTickAsync(stop))
			{
				await using var scope = scopes.CreateAsyncScope();
				var services = scope.ServiceProvider;
				using var tenant = services.GetRequiredService<ICurrentTenant>().Change(claim.TenantId);
				using var unit = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
				var now = new DateTimeOffset(services.GetRequiredService<IClock>().Now.ToUniversalTime());
				var renewed = await services.GetRequiredService<INotificationQueueRepository>()
					.RenewDispatchAsync(claim.Id, claim.LeaseId, now, LeaseDuration, stop);
				await unit.CompleteAsync(stop);
				if (!renewed) return;
			}
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			logger.LogWarning("Notification lease renewal stopped for school {TenantId}, dispatch {DispatchId}.", claim.TenantId, claim.Id);
		}
	}
}
