using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public class HrReportingRetentionWorker : AsyncPeriodicBackgroundWorkerBase
{
	public HrReportingRetentionWorker(AbpAsyncTimer timer, IServiceScopeFactory scopeFactory) : base(timer, scopeFactory) { Timer.Period = 60 * 60 * 1000; }
	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var tenant = context.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var tenants = await context.ServiceProvider.GetRequiredService<ITenantRepository>().GetListAsync();
		foreach (var school in tenants)
		{
			try
			{
				using var changeTenant = tenant.Change(school.Id);
				await context.ServiceProvider.GetRequiredService<HrReportingRetentionService>().PurgeAsync();
			}
			catch (Exception exception) { context.ServiceProvider.GetRequiredService<ILogger<HrReportingRetentionWorker>>().LogError(exception, "Report retention failed for tenant {TenantId}", school.Id); }
		}
	}
}