using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Students.Records;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class StudentDashboardPreviewRefreshWorker : AsyncPeriodicBackgroundWorkerBase
{
	public StudentDashboardPreviewRefreshWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<StudentDashboardPreviewRefreshWorker>>();

		try
		{
			if (!StudentDashboardPreviewSchedule.IsScheduledWindowOpen(DateTime.UtcNow))
			{
				return;
			}

			var dashboardAppService = workerContext.ServiceProvider.GetRequiredService<StudentDashboardAppService>();
			var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
			var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();

			var tenants = await tenantRepository.GetListAsync();
			foreach (var tenant in tenants)
			{
				using (currentTenant.Change(tenant.Id))
				using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
				{
					await dashboardAppService.RefreshExistingPreviewsAsync();
					await uow.CompleteAsync();
				}
			}

			logger.LogInformation(
				"StudentDashboardPreviewRefreshWorker: queued daily dashboard preview refresh jobs for {TenantCount} tenants at {Time}.",
				tenants.Count,
				DateTime.UtcNow);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "StudentDashboardPreviewRefreshWorker: failed to queue daily dashboard refresh jobs.");
		}
	}
}
