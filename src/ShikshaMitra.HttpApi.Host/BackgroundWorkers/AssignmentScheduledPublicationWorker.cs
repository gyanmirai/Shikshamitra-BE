using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public class AssignmentScheduledPublicationWorker : AsyncPeriodicBackgroundWorkerBase
{
	public AssignmentScheduledPublicationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<AssignmentScheduledPublicationWorker>>();
		var publicationService = workerContext.ServiceProvider.GetRequiredService<AssignmentScheduledPublicationService>();

		try
		{
			var publishedCount = await publicationService.PublishDueSchedulesAsync(
				DateTime.UtcNow, workerContext.CancellationToken);

			if (publishedCount > 0)
			{
				logger.LogInformation(
					"AssignmentScheduledPublicationWorker: published {Count} due division schedule(s).",
					publishedCount);
			}
		}
		catch (OperationCanceledException) when (workerContext.CancellationToken.IsCancellationRequested)
		{
			// Committed assignments stay published; remaining schedules are retried next run.
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "AssignmentScheduledPublicationWorker: failed to publish due schedules.");
		}
	}
}
