using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Communication.Notifications;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public abstract class AcademicNotificationWorker : AsyncPeriodicBackgroundWorkerBase
{
	private const int SchoolPageSize = 16;
	private readonly IServiceScopeFactory _scopes;
	private int _nextSchool;

	protected AcademicNotificationWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes)
	{
		_scopes = scopes;
		Timer.Period = 30_000;
	}

	protected abstract Task RunSchoolAsync(IServiceProvider services, CancellationToken cancellationToken);

	protected virtual Task RunWhilePausedAsync(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken) =>
		Task.CompletedTask;

	protected static TimeSpan SchoolTurnBudget(IConfiguration configuration) =>
		TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int>("Notifications:QueueWorker:SchoolTurnSeconds", 45), 15, 900));

	protected sealed override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
		var workerName = GetType().Name;
		var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(workerName);
		if (!NotificationQueueWorkerConfiguration.IsEnabled(configuration))
		{
			await RunWhilePausedAsync(configuration, logger, context.CancellationToken);
			return;
		}
		var timeLimit = SchoolTurnBudget(configuration);
		var schools = context.ServiceProvider.GetRequiredService<NotificationWorkerTenants>();
		try
		{
			var page = await schools.ReadPageAsync(_nextSchool, SchoolPageSize, context.CancellationToken);
			_nextSchool = page.NextOffset;
			await Parallel.ForEachAsync(page.TenantIds, new ParallelOptions {
				MaxDegreeOfParallelism = 4, CancellationToken = context.CancellationToken
			}, (id, token) => ProcessSchoolAsync(id, timeLimit, logger, token, RunSchoolAsync));
		}
		catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			logger.LogWarning("{Worker} could not read or complete its school page.", workerName);
		}
	}

	protected async ValueTask ProcessSchoolAsync(Guid id, TimeSpan timeLimit, ILogger logger, CancellationToken stopping,
		Func<IServiceProvider, CancellationToken, Task> runSchool)
	{
		using var turn = CancellationTokenSource.CreateLinkedTokenSource(stopping);
		turn.CancelAfter(timeLimit);
		try
		{
			await using var scope = _scopes.CreateAsyncScope();
			var services = scope.ServiceProvider;
			if (!await services.GetRequiredService<NotificationWorkerTenants>().IsActiveAsync(id, turn.Token)) return;
			using var school = services.GetRequiredService<ICurrentTenant>().Change(id);
			await runSchool(services, turn.Token);
		}
		catch (OperationCanceledException) when (turn.IsCancellationRequested)
		{
		}
		catch (Exception)
		{
			logger.LogWarning("{Worker} could not finish the turn for school {TenantId}.", GetType().Name, id);
		}
	}
}
