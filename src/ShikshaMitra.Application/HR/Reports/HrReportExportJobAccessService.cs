using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportJobAccessService(
	IRepository<HrReportExportJob, Guid> exportJobRepository,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	public async Task<List<HrReportExportJobDto>> GetRecentJobsAsync(Guid userId, DateTime now)
	{
		var query = await exportJobRepository.GetQueryableAsync();
		var jobs = await asyncExecuter.ToListAsync(
			query
				.Where(x => x.RequestedByUserId == userId)
				.OrderByDescending(x => x.CreationTime)
				.Take(50));

		return jobs
			.Select(job => HrReportProjection.ToJobDto(job, now))
			.ToList();
	}

	public async Task<HrReportExportJob> GetAuthorizedJobAsync(Guid jobId, Guid userId)
	{
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId && !job.GetSharedUserIds().Contains(userId))
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied);
		}

		return job;
	}

	public async Task<HrReportExportJob> GetOwnedJobAsync(Guid jobId, Guid userId)
	{
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId)
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied);
		}

		return job;
	}
}
