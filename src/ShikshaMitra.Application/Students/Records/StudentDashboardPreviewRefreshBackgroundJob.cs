using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public class StudentDashboardPreviewRefreshBackgroundJob(
	StudentDashboardAppService dashboardAppService,
	ICurrentTenant currentTenant)
	: AsyncBackgroundJob<StudentDashboardPreviewRefreshJobArgs>, ITransientDependency
{
	[UnitOfWork]
	public override async Task ExecuteAsync(StudentDashboardPreviewRefreshJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			await dashboardAppService.ExecutePreviewRefreshAsync(args);
		}
	}
}
