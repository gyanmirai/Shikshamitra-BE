using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Dashboard;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class HrDashboardPreviewRefreshWorker : AsyncPeriodicBackgroundWorkerBase
{
	public HrDashboardPreviewRefreshWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<HrDashboardPreviewRefreshWorker>>();
		var dashboardAppService = workerContext.ServiceProvider.GetRequiredService<HRDashboardAppService>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		await dashboardAppService.RefreshExistingPreviewsAsync();
		await uow.CompleteAsync();

		logger.LogInformation("HrDashboardPreviewRefreshWorker: queued dashboard preview refresh jobs at {Time}.", DateTime.UtcNow);
	}
}
