using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportingRetentionService(IRepository<HrReportRun, Guid> runs, IRepository<HrReportRunPage, Guid> pages,
	IClock clock, IAsyncQueryableExecuter executor, IBlobContainer blobs, IUnitOfWorkManager uows) : ITransientDependency
{
	public async Task PurgeAsync()
	{
		using var uow = uows.Begin(requiresNew: true, isTransactional: true);
		var expired = await executor.ToListAsync((await runs.GetQueryableAsync()).Where(x => x.ExpiresAt <= clock.Now && x.Status != "Expired").OrderBy(x => x.ExpiresAt).Take(100));
		foreach (var run in expired)
		{
			await blobs.DeleteAsync(HrReportingWorkspaceAppService.BlobName(run, "excel"));
			await blobs.DeleteAsync(HrReportingWorkspaceAppService.BlobName(run, "pdf"));
			await pages.DeleteAsync(x => x.RunId == run.Id);
			run.Expire(clock.Now); await runs.UpdateAsync(run);
		}
		await uow.CompleteAsync();
	}
}