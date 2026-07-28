using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

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
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

		try
		{
			using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			var publishedCount = await publicationService.PublishDueSchedulesAsync(DateTime.UtcNow);
			await uow.CompleteAsync();

			if (publishedCount > 0)
			{
				logger.LogInformation(
					"AssignmentScheduledPublicationWorker: published {Count} due division schedule(s).",
					publishedCount);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "AssignmentScheduledPublicationWorker: failed to publish due schedules.");
		}
	}
}
