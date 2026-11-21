using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Reservations;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class LibraryNotificationReminderWorker : AsyncPeriodicBackgroundWorkerBase
{
	public LibraryNotificationReminderWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 15 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<LibraryNotificationReminderWorker>>();
		var reminderService = workerContext.ServiceProvider.GetRequiredService<LibraryNotificationReminderService>();
		var reservations = workerContext.ServiceProvider.GetRequiredService<LibraryReservationCoordinator>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var tenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var schools = await workerContext.ServiceProvider.GetRequiredService<ITenantRepository>().GetListAsync();

		foreach (var school in schools)
		{
			try
			{
				using var schoolScope = tenant.Change(school.Id);
				using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
				await reservations.RebalanceTenantAsync();
				var histories = await reminderService.ProcessDueRemindersAsync();
				await uow.CompleteAsync();
				if (histories.Count > 0)
					logger.LogInformation("Recorded {Count} Library reminder intent(s) for tenant {TenantId}.", histories.Count, school.Id);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Library reminder processing failed for tenant {TenantId}.", school.Id);
			}
		}
	}
}
