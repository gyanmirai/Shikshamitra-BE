using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Linq;

namespace ShikshaMitra.TenantSettings.Reports;

public class InstituteReportRetention(IInstituteReportJobRepository jobs, IBlobContainer blobs,
	IBackgroundJobManager background, IAsyncQueryableExecuter executor) : ITransientDependency
{
	[UnitOfWork]
	public virtual async Task PurgeAsync()
	{
		var cutoff = DateTime.UtcNow;
		var query = await jobs.GetQueryableAsync();
		foreach (var job in await executor.ToListAsync(query.Where(x => x.ExpiresAt <= cutoff && x.BlobName != null).OrderBy(x => x.ExpiresAt).Take(100)))
		{
			await blobs.DeleteAsync(job.BlobName!);
			job.RemoveExpiredFile();
			await jobs.UpdateAsync(job);
		}
		var queueTimeout = cutoff.AddDays(-1);
		var legacyTimeout = cutoff.AddHours(-1);
		foreach (var job in await executor.ToListAsync(query.Where(x => (x.Status == "Queued" && x.CreationTime < queueTimeout)
			|| (x.Status == "Processing" && (x.LeaseExpiresAt <= cutoff || (x.LeaseExpiresAt == null && x.CreationTime < legacyTimeout))))
			.OrderBy(x => x.CreationTime).Take(100)))
		{
			if (job.Status == "Processing" && job.Attempts < 3 && job.ExpiresAt > cutoff)
			{
				if (await jobs.RequeueAsync(job.Id, cutoff)) await background.EnqueueAsync(new InstituteReportJobArgs { JobId = job.Id, TenantId = job.TenantId!.Value });
			}
			else await jobs.ExpireAsync(job.Id, cutoff);
		}
	}
}
