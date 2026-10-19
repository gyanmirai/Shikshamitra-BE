using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportJobAccessService(
	IHrReportExportJobRepository exportJobRepository,
	HrReportExportScopeAuthorizationService scopeAuthorizationService, HrReportingAccess access)
	: ITransientDependency
{
	public async Task<List<HrReportExportJobDto>> GetRecentJobsAsync(
		Guid userId,
		DateTime now,
		HrReportExportListRequestDto input)
	{
		var jobs = await exportJobRepository.GetRecentAuthorizedJobsAsync(
			userId,
			50,
			input.RequestedFrom,
			input.RequestedTo);
		var resolvedUserScope = await scopeAuthorizationService.ResolveCurrentScopeAsync(userId);
		var authorizedJobs = new List<HrReportExportJobDto>();
		foreach (var job in jobs)
		{
			var source = HrReportStoredExportRequestSerializer.DeserializeRequest(job.RequestJson)?.DataSource;
			if (source is not ("payroll_register" or "payroll_statement") || !await access.CanReadAsync(source, userId)) continue;
			if (resolvedUserScope != null &&
				scopeAuthorizationService.CanResolvedScopeAccessExport(job, resolvedUserScope))
			{
				authorizedJobs.Add(HrReportProjection.ToJobDto(job, now));
			}
		}

		return authorizedJobs;
	}

	public async Task<HrReportExportJob> GetAuthorizedJobAsync(Guid jobId, Guid userId)
	{
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId &&
			!job.GetSharedUserIds().Contains(userId))
		{
			throw new AbpAuthorizationException("Access denied: HR report export is not shared with this user.");
		}

		await scopeAuthorizationService.EnsureRecipientCanAccessExportAsync(job, userId);
		var source = HrReportStoredExportRequestSerializer.DeserializeRequest(job.RequestJson)?.DataSource;
		if (source is not ("payroll_register" or "payroll_statement")) throw new AbpAuthorizationException();
		await access.EnsureAsync(source, userId);
		return job;
	}

	public async Task<HrReportExportJob> GetOwnedJobAsync(Guid jobId, Guid userId)
	{
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId)
		{
			throw new AbpAuthorizationException("Access denied: only the export owner can perform this action.");
		}
		var source = HrReportStoredExportRequestSerializer.DeserializeRequest(job.RequestJson)?.DataSource;
		if (source is not ("payroll_register" or "payroll_statement")) throw new AbpAuthorizationException();
		await access.EnsureAsync(source, userId);

		return job;
	}
}