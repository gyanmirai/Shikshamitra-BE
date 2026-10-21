using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.Reports;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;

namespace ShikshaMitra.BackgroundWorkers;

public class InstituteReportRetentionWorker : AsyncPeriodicBackgroundWorkerBase
{
	public InstituteReportRetentionWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : base(timer, scopes) { Timer.Period = 3600000; }
	protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext context)
	{
		var tenant = context.ServiceProvider.GetRequiredService<ICurrentTenant>();
		foreach (var school in await context.ServiceProvider.GetRequiredService<ITenantRepository>().GetListAsync())
			try
			{
				using var change = tenant.Change(school.Id);
				await context.ServiceProvider.GetRequiredService<InstituteReportRetention>().PurgeAsync();
			}
			catch (Exception exception) { context.ServiceProvider.GetRequiredService<ILogger<InstituteReportRetentionWorker>>().LogError(exception, "Institute report retention failed for {TenantId}", school.Id); }
	}
}
