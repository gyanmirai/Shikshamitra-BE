using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.AttendanceAndLeaves.YearEndSettlements;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.BackgroundWorkers;

public class LeaveYearEndSettlementPreviewWorker : AsyncPeriodicBackgroundWorkerBase
{
	public LeaveYearEndSettlementPreviewWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 6 * 60 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<LeaveYearEndSettlementPreviewWorker>>();
		var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
		var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

		try
		{
			var tenants = await tenantRepository.GetListAsync();
			var processed = 0;
			foreach (var tenant in tenants)
			{
				using (currentTenant.Change(tenant.Id))
				using (var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
				{
					var academicYearRepository = workerContext.ServiceProvider.GetRequiredService<IAcademicYearRepository>();
					var runRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<LeaveYearEndSettlementRun, Guid>>();
					var asyncExecuter = workerContext.ServiceProvider.GetRequiredService<IAsyncQueryableExecuter>();
					var workflow = workerContext.ServiceProvider.GetRequiredService<LeaveYearEndSettlementWorkflow>();

					var today = DateTime.Today;
					var yearQuery = await academicYearRepository.GetQueryableAsync();
					var endedYears = await asyncExecuter.ToListAsync(yearQuery
						.Where(x => x.EndDate.Date < today)
						.OrderByDescending(x => x.EndDate));
					var runQuery = await runRepository.GetQueryableAsync();
					var finalizedYearIds = (await asyncExecuter.ToListAsync(runQuery.Where(x =>
							x.Status == LeaveYearEndSettlementRunStatus.Finalized)))
						.Select(x => x.AcademicYearId)
						.ToHashSet();

					foreach (var year in endedYears.Where(x => !finalizedYearIds.Contains(x.Id)).Take(3))
					{
						await workflow.PreviewAsync(year.Id);
						processed++;
					}

					await uow.CompleteAsync();
				}
			}

			if (processed > 0)
			{
				logger.LogInformation(
					"LeaveYearEndSettlementPreviewWorker: refreshed {Count} draft leave year-end settlement run(s).",
					processed);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "LeaveYearEndSettlementPreviewWorker: failed to refresh draft settlements.");
		}
	}
}
