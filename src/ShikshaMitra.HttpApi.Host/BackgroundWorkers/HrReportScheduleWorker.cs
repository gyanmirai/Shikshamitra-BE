using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public class HrReportScheduleWorker : AsyncPeriodicBackgroundWorkerBase
{
	public HrReportScheduleWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
		: base(timer, serviceScopeFactory)
	{
		Timer.Period = 15 * 60 * 1000;
	}

	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
	{
		var logger = workerContext.ServiceProvider.GetRequiredService<ILogger<HrReportScheduleWorker>>();
		var scheduleService = workerContext.ServiceProvider.GetRequiredService<HrReportScheduleService>();

		try
		{
			var processedCount = await scheduleService.ProcessDueSchedulesAsync();

			if (processedCount > 0)
			{
				logger.LogInformation("HrReportScheduleWorker: processed {Count} due HR report schedule(s).", processedCount);
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "HrReportScheduleWorker: failed to process due HR report schedules.");
		}
	}
}
