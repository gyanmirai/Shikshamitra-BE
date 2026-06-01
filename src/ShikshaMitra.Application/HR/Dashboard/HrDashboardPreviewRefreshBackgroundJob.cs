using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Dashboard;

public class HrDashboardPreviewRefreshBackgroundJob(
	HRDashboardAppService dashboardAppService,
	ICurrentTenant currentTenant)
	: AsyncBackgroundJob<HrDashboardPreviewRefreshJobArgs>, ITransientDependency
{
	[UnitOfWork]
	public override async Task ExecuteAsync(HrDashboardPreviewRefreshJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			await dashboardAppService.ExecutePreviewRefreshAsync(args);
		}
	}
}
