using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportJobAccessService(
	IHrReportExportJobRepository exportJobRepository)
	: ITransientDependency
{
	public async Task<List<HrReportExportJobDto>> GetRecentJobsAsync(Guid userId, DateTime now)
	{
		var jobs = await exportJobRepository.GetRecentAuthorizedJobsAsync(userId, 50);
		return jobs
			.Select(job => HrReportProjection.ToJobDto(job, now))
			.ToList();
	}

	public async Task<HrReportExportJob> GetAuthorizedJobAsync(Guid jobId, Guid userId)
	{
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId && !job.GetSharedUserIds().Contains(userId))
		{
			throw new AbpAuthorizationException("Access denied: HR report export is not shared with this user.");
		}

		return job;
	}

	public async Task<HrReportExportJob> GetOwnedJobAsync(Guid jobId, Guid userId)
	{
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId)
		{
			throw new AbpAuthorizationException("Access denied: only the export owner can perform this action.");
		}

		return job;
	}
}
