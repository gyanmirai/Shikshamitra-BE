using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Library.Notifications;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class LibraryNotificationReminderWorker : AsyncPeriodicBackgroundWorkerBase
{
	public LibraryNotificationReminderWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 24 * 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<LibraryNotificationReminderWorker>>();
		var reminderService = workerContext.ServiceProvider.GetRequiredService<LibraryNotificationReminderService>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

		try
		{
			using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
			var histories = await reminderService.ProcessDueRemindersAsync(DateTime.UtcNow.Date);
			await uow.CompleteAsync();

			if (histories.Count > 0)
			{
				logger.LogInformation("LibraryNotificationReminderWorker: recorded {Count} Library notification reminder history row(s).", histories.Count);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "LibraryNotificationReminderWorker: failed to process Library reminder notifications.");
		}
	}
}
