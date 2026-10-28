using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.Reports;

[RemoteService(false)]
public partial class InstituteReportAppService(InstituteReportData data, InstituteReportAccess access,
	InstituteReportPlanner planner, InstituteReportApproval approval, IRepository<InstituteReportJob, Guid> jobs,
	IBackgroundJobManager background, IBlobContainer blobs, IUserScopeService scopes) : ApplicationService, IInstituteReportAppService
{
	private Guid User => CurrentUser.Id ?? throw new AbpAuthorizationException();
	private Guid Tenant => CurrentTenant.Id ?? throw new AbpAuthorizationException("Select a school workspace.");
	public async Task<InstituteReportOptions> GetOptionsAsync() { await access.EnsureAsync(User); return await data.OptionsAsync(User); }
	public async Task<AcademicReportPreviewDto> PreviewAsync(InstituteReportRequest input) { await access.EnsureAsync(User, input.ReportType); return await data.GenerateAsync(input, User); }
	public async Task<InstituteReportJobDto> CreateExportAsync(InstituteReportRequest input) => await QueueAsync(input, false);
	private async Task<InstituteReportJobDto> QueueAsync(InstituteReportRequest input, bool dynamic)
	{
		await access.EnsureAsync(User, input.ReportType, generate: true);
		InstituteReportCatalog.Validate(input);
		var job = new InstituteReportJob(GuidGenerator.Create(), Tenant, User, JsonSerializer.Serialize(input), dynamic);
		await jobs.InsertAsync(job, autoSave: true);
		await background.EnqueueAsync(new InstituteReportJobArgs { JobId = job.Id, TenantId = Tenant });
		return Map(job);
	}
	public async Task<List<InstituteReportJobDto>> GetExportsAsync()
	{
		await access.EnsureAsync(User);
		var query = (await jobs.GetQueryableAsync()).Where(x => x.TenantId == Tenant && x.RequestedBy == User).OrderByDescending(x => x.CreationTime);
		var list = new List<InstituteReportJobDto>();
		foreach (var job in await AsyncExecuter.ToListAsync(query))
			if (await access.CanReadAsync(User, Read(job).ReportType)) list.Add(Map(job));
		return list;
	}
	public async Task<InstituteReportJobDto> GetExportAsync(Guid jobId) => Map(await AuthorizedJobAsync(jobId));
	public async Task<AcademicReportDownloadDto> DownloadAsync(Guid jobId)
	{
		await access.EnsureAsync(User, refresh: true);
		var job = await AuthorizedJobAsync(jobId);
		if (job.Status != "Completed" || job.BlobName == null || job.ExpiresAt <= Clock.Now) throw new BusinessException("InstituteReports:FileUnavailable", "The report is not ready or has expired. Generate it again if needed.");
		var request = Read(job);
		await access.EnsureAsync(User, request.ReportType);
		// A scope change must invalidate a previously generated academic file, even if its report grant remains.
		if (InstituteReportCatalog.RequiresScopeStamp(request.ReportType))
		{
			var hash = InstituteReportFiles.ScopeHash(await scopes.GetUserScopeAsync(User));
			if (!job.BlobName.Contains(hash, StringComparison.Ordinal)) throw new AbpAuthorizationException("Report data or access changed. Generate the report again.");
		}
		var bytes = await blobs.GetAllBytesOrNullAsync(job.BlobName) ?? throw new BusinessException("InstituteReports:FileUnavailable");
		return new AcademicReportDownloadDto { Bytes = bytes, ContentType = InstituteReportFiles.ContentType(request.Format), FileName = Map(job).FileName! };
	}
	public async Task<InstituteAiPlanResult> PlanAsync(InstituteAiPlanInput input)
	{
		await access.EnsureAsync(User, generate: true);
		var result = await planner.PlanAsync(input, User);
		if (result.Plan != null)
		{
			await access.EnsureAsync(User, result.Plan.ReportType, generate: true);
			result.ApprovalToken = approval.Create(User, Tenant, result.Plan);
		}
		return result;
	}
	public async Task<AcademicReportPreviewDto> AiPreviewAsync(InstituteAiApprovedRequest input)
	{
		await access.EnsureAsync(User, input.Plan.ReportType, generate: true);
		approval.Validate(input.ApprovalToken, User, Tenant, input.Plan);
		return await data.GenerateAsync(input.Plan, User);
	}
	public async Task<InstituteReportJobDto> AiExportAsync(InstituteAiApprovedRequest input)
	{
		await access.EnsureAsync(User, input.Plan.ReportType, generate: true);
		approval.Validate(input.ApprovalToken, User, Tenant, input.Plan);
		return await QueueAsync(input.Plan, true);
	}
	private async Task<InstituteReportJob> AuthorizedJobAsync(Guid id)
	{
		await access.EnsureAsync(User);
		var job = await jobs.FindAsync(x => x.Id == id && x.TenantId == Tenant && x.RequestedBy == User) ?? throw new AbpAuthorizationException();
		await access.EnsureAsync(User, Read(job).ReportType);
		return job;
	}
	internal static InstituteReportRequest Read(InstituteReportJob job) => JsonSerializer.Deserialize<InstituteReportRequest>(job.RequestJson) ?? throw new BusinessException("InstituteReports:InvalidRequest");
	internal static InstituteReportJobDto Map(InstituteReportJob job)
	{
		var request = Read(job); var title = InstituteReportCatalog.Get(request.ReportType).Label;
		return new InstituteReportJobDto { Id = job.Id, ReportType = title, Format = request.Format,
			Status = job.ExpiresAt <= DateTime.UtcNow ? "Expired" : job.Status, Request = request, IsDynamic = job.IsDynamic,
			CreationTime = job.CreationTime, CompletedAt = job.CompletedAt, ExpiresAt = job.ExpiresAt, ErrorMessage = job.ErrorMessage,
			FileName = job.Status == "Completed" ? $"{title.Replace(' ', '_')}_{job.CreationTime:yyyyMMdd_HHmmss}.{(request.Format == "excel" ? "xlsx" : request.Format)}" : null };
	}
}
