using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
public class HrReportExportService(
	IRepository<HrReportExportJob, Guid> exportJobRepository,
	IBackgroundJobManager backgroundJobManager,
	IBlobContainer blobContainer,
	IGuidGenerator guidGenerator,
	HrReportExportJobAccessService jobAccessService,
	HrAuditWriter auditWriter) : ApplicationService, ITransientDependency
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input)
	{
		var userId = GetRequiredUserId();
		return await CreateExportForUserAsync(input, userId, CurrentTenant.Id);
	}

	public async Task<HrReportExportCreateResponseDto> CreateExportForUserAsync(
		HrReportRequestDto input,
		Guid userId,
		Guid? tenantId,
		IReadOnlyCollection<Guid>? sharedUserIds = null)
	{
		var requestJson = JsonSerializer.Serialize(input, JsonOptions);
		var job = new HrReportExportJob(
			guidGenerator.Create(),
			tenantId,
			userId,
			input.ReportType,
			HrReportProjection.NormalizeFormat(input.Format),
			JsonSerializer.Serialize(input.Filters ?? new HrReportFilterDto(), JsonOptions),
			requestJson,
			Clock.Now.AddDays(7));
		if (sharedUserIds is { Count: > 0 })
		{
			job.ShareWith(sharedUserIds.Where(x => x != Guid.Empty && x != userId).Distinct().ToList());
		}

		await exportJobRepository.InsertAsync(job, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new HrReportExportJobArgs
		{
			JobId = job.Id,
			TenantId = tenantId,
			UserId = userId,
			Request = input
		});
		await WriteAuditAsync(job.Id, "export_queued", $"Queued HR report export {job.ReportType}", new
		{
			job.ReportType,
			job.Format,
			input.DataSource,
			input.Columns
		});

		return new HrReportExportCreateResponseDto
		{
			JobId = job.Id,
			Status = job.Status
		};
	}

	public async Task<List<HrReportExportJobDto>> GetExportsAsync()
	{
		var userId = GetRequiredUserId();
		return await jobAccessService.GetRecentJobsAsync(userId, Clock.Now);
	}

	public async Task<HrReportExportJobDto> GetExportAsync(Guid jobId) =>
		HrReportProjection.ToJobDto(await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId()), Clock.Now);

	public async Task<HrReportExportJobDto> CancelExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId());
		if (job.Status is HrReportExportStatus.Queued or HrReportExportStatus.Running)
		{
			job.MarkCancelled(Clock.Now);
			await exportJobRepository.UpdateAsync(job, autoSave: true);
			await WriteAuditAsync(job.Id, "export_cancelled", $"Cancelled HR report export {job.ReportType}", new { job.ReportType, job.Format });
		}

		return HrReportProjection.ToJobDto(job, Clock.Now);
	}

	public async Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId());
		var request = DeserializeRequest(job);
		job.MarkQueued(Clock.Now.AddDays(7));
		await exportJobRepository.UpdateAsync(job, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new HrReportExportJobArgs
		{
			JobId = job.Id,
			TenantId = job.TenantId,
			UserId = job.RequestedByUserId,
			Request = request
		});
		await WriteAuditAsync(job.Id, "export_retried", $"Retried HR report export {job.ReportType}", new { job.ReportType, job.Format });

		return new HrReportExportCreateResponseDto { JobId = job.Id, Status = job.Status };
	}

	public async Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId());
		var request = DeserializeRequest(job);
		var created = await CreateExportAsync(request);
		await WriteAuditAsync(job.Id, "export_regenerated", $"Regenerated HR report export {job.ReportType}", new { OriginalJobId = job.Id, created.JobId });
		return created;
	}

	public async Task<HrReportExportJobDto> ShareExportAsync(Guid jobId, HrReportShareExportRequestDto input)
	{
		var job = await jobAccessService.GetOwnedJobAsync(jobId, GetRequiredUserId());
		var userIds = input.UserIds.Where(x => x != Guid.Empty).Distinct().ToList();
		job.ShareWith(userIds);
		await exportJobRepository.UpdateAsync(job, autoSave: true);
		await WriteAuditAsync(job.Id, "export_shared", $"Shared HR report export {job.ReportType}", new { job.ReportType, SharedUserIds = userIds });
		return HrReportProjection.ToJobDto(job, Clock.Now);
	}

	public async Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId());
		if (job.Status != HrReportExportStatus.Completed || string.IsNullOrWhiteSpace(job.BlobName))
		{
			throw new BusinessException(HRErrorCodes.ReportExportNotReady)
				.WithData("status", job.Status);
		}
		if (job.ExpiresAt <= Clock.Now)
		{
			throw new BusinessException(HRErrorCodes.ReportExportNotReady)
				.WithData("status", "Expired");
		}

		await using var stream = await blobContainer.GetAsync(job.BlobName);
		await using var memory = new MemoryStream();
		await stream.CopyToAsync(memory);
		var format = HrReportProjection.NormalizeFormat(job.Format);
		return new HrReportDownloadDto
		{
			Bytes = memory.ToArray(),
			ContentType = format == HrReportFormat.Pdf
				? "application/pdf"
				: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			FileName = HrReportProjection.BuildFileName(job.ReportType, format)
		};
	}

	private static HrReportRequestDto DeserializeRequest(HrReportExportJob job)
	{
		var request = JsonSerializer.Deserialize<HrReportRequestDto>(job.RequestJson, JsonOptions);
		if (request != null)
		{
			return request;
		}

		var filters = JsonSerializer.Deserialize<HrReportFilterDto>(job.FilterJson, JsonOptions) ?? new HrReportFilterDto();
		return new HrReportRequestDto
		{
			ReportType = job.ReportType,
			Format = job.Format,
			Filters = filters
		};
	}

	private Task WriteAuditAsync(Guid entityId, string action, string summary, object? context) =>
		auditWriter.WriteAsync(
			null,
			"hr_report_export",
			entityId,
			"HR Report Export",
			action,
			summary,
			context: context);

	private Guid GetRequiredUserId() =>
		CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);
}
