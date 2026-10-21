using System;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportRetention(IRepository<InstituteReportJob, Guid> jobs, IBlobContainer blobs) : ITransientDependency
{
	[UnitOfWork]
	public virtual async Task PurgeAsync()
	{
		var cutoff = DateTime.UtcNow;
		foreach (var job in await jobs.GetListAsync(x => x.ExpiresAt <= cutoff && x.BlobName != null))
		{
			await blobs.DeleteAsync(job.BlobName!);
			job.RemoveExpiredFile();
			await jobs.UpdateAsync(job);
		}
		var stale = cutoff.AddHours(-1);
		foreach (var job in await jobs.GetListAsync(x => x.CreationTime < stale && (x.Status == "Processing" || x.Status == "Queued")))
		{
			job.Fail(); await jobs.UpdateAsync(job);
		}
	}
}
