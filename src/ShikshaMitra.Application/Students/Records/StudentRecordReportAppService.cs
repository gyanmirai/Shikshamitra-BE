using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Reports.View)]
public class StudentRecordReportAppService(IRepository<StudentRecordReportJob, Guid> jobs,
	StudentRecordReportGenerator generator, StudentRecordReportAuthority authority, IBlobContainer blobs)
	: ApplicationService, IStudentRecordReportAppService
{
	public Task<StudentRecordReportPreviewDto> PreviewAsync(StudentRecordReportInput input) => generator.GeneratePageAsync(input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add)]
	public async Task<StudentRecordReportJobDto> CreateAsync(StudentRecordReportInput input)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		if (!CurrentTenant.Id.HasValue || string.IsNullOrWhiteSpace(input.ClientKey)) throw new BusinessException("Students:Records:InvalidReport");
		await generator.GeneratePageAsync(new() { ReportType = input.ReportType, AcademicYearId = input.AcademicYearId, StudentId = input.StudentId, Status = input.Status, MaxResultCount = 1 });
		input.SkipCount = 0; input.MaxResultCount = 100;
		var json = StudentRecordPayloadService.Canonical(JsonSerializer.Serialize(input, StudentRecordPayloadService.JsonOptions));
		var existing = await jobs.FindAsync(x => x.TenantId == CurrentTenant.Id && x.RequestedBy == CurrentUser.Id && x.ClientKey == input.ClientKey);
		if (existing != null)
		{
			if (existing.RequestJson != json) throw new BusinessException("Students:Records:IdempotencyConflict");
			return await MapAsync(existing);
		}
		var job = new StudentRecordReportJob(GuidGenerator.Create(), CurrentTenant.Id.Value, CurrentUser.GetId(), input.ClientKey,
			input.ReportType, json, await authority.CaptureAsync(input.ReportType), new DateTimeOffset(Clock.Now.ToUniversalTime()).AddDays(7));
		await jobs.InsertAsync(job, autoSave: true);
		return await MapAsync(job);
	}
	public async Task<PagedResultDto<StudentRecordReportJobDto>> GetListAsync(PagedResultRequestDto input)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		var query = (await jobs.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id && x.RequestedBy == CurrentUser.Id);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip(input.SkipCount).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		var result = new System.Collections.Generic.List<StudentRecordReportJobDto>();
		var authorityHashes = new System.Collections.Generic.Dictionary<string, string?>();
		foreach (var row in rows) result.Add(await MapAsync(row, authorityHashes));
		return new(count, result);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Reports.Add)]
	public async Task<StudentRecordReportJobDto> CancelAsync(Guid id)
	{
		var job = await GetOwnedAsync(id); job.Cancel(); await jobs.UpdateAsync(job, autoSave: true); return await MapAsync(job);
	}
	public async Task<StudentPhotoDownloadDto> DownloadAsync(Guid id)
	{
		var job = await GetOwnedAsync(id);
		if (job.Status != "Completed" || job.ExpiresAt <= DateTimeOffset.UtcNow || job.BlobName == null) throw new BusinessException("Students:Records:ReportNotReady");
		if (job.AuthorityHash != await authority.CaptureAsync(job.ReportType)) throw new BusinessException("Students:Records:ReportAuthorityChanged");
		return new() { Content = await blobs.GetAllBytesAsync(job.BlobName), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			FileName = $"student-{job.ReportType.ToLowerInvariant()}-{job.CreationTime:yyyy-MM-dd}.xlsx" };
	}
	private async Task<StudentRecordReportJob> GetOwnedAsync(Guid id)
	{
		await LazyServiceProvider.LazyGetRequiredService<StudentRecordsActorService>().EnsureActiveAsync();
		return await jobs.GetAsync(x => x.Id == id && x.TenantId == CurrentTenant.Id && x.RequestedBy == CurrentUser.Id);
	}
	private async Task<StudentRecordReportJobDto> MapAsync(StudentRecordReportJob job, System.Collections.Generic.Dictionary<string, string?>? authorityHashes = null)
	{
		var canDownload = false;
		if (job.Status == "Completed" && job.ExpiresAt > DateTimeOffset.UtcNow)
		{
			if (authorityHashes == null || !authorityHashes.TryGetValue(job.ReportType, out var currentHash))
			{
				try { currentHash = await authority.CaptureAsync(job.ReportType); }
				catch (Volo.Abp.Authorization.AbpAuthorizationException) { currentHash = null; }
				if (authorityHashes != null) authorityHashes[job.ReportType] = currentHash;
			}
			canDownload = job.AuthorityHash == currentHash;
		}
		return new() { Id = job.Id, ReportType = job.ReportType, Status = job.ExpiresAt <= DateTimeOffset.UtcNow ? "Expired" : job.Status,
			ErrorCode = job.ErrorCode, RowCount = job.RowCount, CreationTime = job.CreationTime, ExpiresAt = job.ExpiresAt,
			CanDownload = canDownload, Request = JsonSerializer.Deserialize<StudentRecordReportInput>(job.RequestJson, StudentRecordPayloadService.JsonOptions)! };
	}
}
