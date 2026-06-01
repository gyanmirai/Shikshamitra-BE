using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Dashboard;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class HrDashboardPreviewRefreshWorker : AsyncPeriodicBackgroundWorkerBase
{
	private static readonly TimeSpan StaleRunAfter = TimeSpan.FromHours(2);

	public HrDashboardPreviewRefreshWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<HrDashboardPreviewRefreshWorker>>();
		Guid? runId = null;
		try
		{
			var dashboardAppService = workerContext.ServiceProvider.GetRequiredService<HRDashboardAppService>();
			var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
			var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
			var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
			var runRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<HrDashboardRefreshRun, Guid>>();

			var nowLocal = GetIndiaNow();
			if (nowLocal.Hour < 6)
			{
				return;
			}

			var runKey = $"hr-dashboard-daily:{nowLocal:yyyy-MM-dd}";
			var nowUtc = DateTime.UtcNow;
			HrDashboardRefreshRun? run;
			using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
			{
				run = await runRepository.FirstOrDefaultAsync(x => x.RunKey == runKey);
				if (run != null &&
					(run.Status == HrDashboardRefreshRunStatus.Completed ||
					(run.Status == HrDashboardRefreshRunStatus.Running && nowUtc - run.StartedAt < StaleRunAfter)))
				{
					return;
				}

				if (run == null)
				{
					run = new HrDashboardRefreshRun(Guid.NewGuid(), runKey, nowUtc);
					await runRepository.InsertAsync(run, autoSave: true);
				}
				else
				{
					run.Restart(nowUtc);
					await runRepository.UpdateAsync(run, autoSave: true);
				}

				runId = run.Id;
				await uow.CompleteAsync();
			}

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

			using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
			{
				run.MarkCompleted(tenants.Count, DateTime.UtcNow);
				await runRepository.UpdateAsync(run, autoSave: true);
				await uow.CompleteAsync();
			}

			logger.LogInformation(
				"HrDashboardPreviewRefreshWorker: queued daily dashboard preview refresh jobs for {TenantCount} tenants at {Time}.",
				tenants.Count,
				DateTime.UtcNow);
		}
		catch (Exception ex)
		{
			if (runId.HasValue)
			{
				await MarkRunFailedAsync(workerContext.ServiceProvider, runId.Value, ex);
			}

			logger.LogError(ex, "HrDashboardPreviewRefreshWorker: failed to queue daily dashboard refresh jobs.");
		}
	}

	private static async Task MarkRunFailedAsync(IServiceProvider serviceProvider, Guid runId, Exception exception)
	{
		var unitOfWorkManager = serviceProvider.GetRequiredService<IUnitOfWorkManager>();
		var runRepository = serviceProvider.GetRequiredService<IRepository<HrDashboardRefreshRun, Guid>>();

		using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
		{
			var run = await runRepository.FindAsync(runId);
			if (run != null)
			{
				run.MarkFailed(exception.Message, DateTime.UtcNow);
				await runRepository.UpdateAsync(run, autoSave: true);
			}

			await uow.CompleteAsync();
		}
	}

	private static DateTime GetIndiaNow()
	{
		try
		{
			return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
		}
		catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
		{
			return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		}
		catch
		{
			return DateTime.UtcNow.AddHours(5.5);
		}
	}
}
