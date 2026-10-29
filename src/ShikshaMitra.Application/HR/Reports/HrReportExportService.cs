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
	HrReportExportRecipientService recipientService,
	HrReportExportScopeAuthorizationService scopeAuthorizationService,
	HrReportRequestValidationService requestValidationService,
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
		IReadOnlyCollection<Guid>? sharedUserIds = null,
		string? sourceAction = null,
		Guid? parentJobId = null)
	{
		requestValidationService.Validate(input);
		if (PayrollIssuedReportBoundary.IsSalaryDocument(input.DataSource)) input.Filters.IncludeDrafts = false;
		input.SourceAction = string.IsNullOrWhiteSpace(sourceAction) ? "manualExport" : sourceAction;
		input.ParentJobId = parentJobId;
		var scopeSnapshot = await scopeAuthorizationService.CaptureAsync(userId, tenantId);
		var recipientUserIds = recipientService.Normalize(sharedUserIds, userId);
		await recipientService.ValidateAsync(recipientUserIds, scopeSnapshot);
		var generationId = guidGenerator.Create();
		var requestJson = HrReportStoredExportRequestSerializer.Serialize(
			input,
			scopeSnapshot,
			generationId);
		var job = new HrReportExportJob(
			guidGenerator.Create(),
			tenantId,
			userId,
			input.ReportType,
			HrReportProjection.NormalizeFormat(input.Format),
			JsonSerializer.Serialize(input.Filters ?? new HrReportFilterDto(), JsonOptions),
			requestJson,
			Clock.Now.AddDays(7));
		if (recipientUserIds.Count > 0)
		{
			job.ShareWith(recipientUserIds);
		}

		await exportJobRepository.InsertAsync(job, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new HrReportExportJobArgs
		{
			JobId = job.Id,
			TenantId = tenantId,
			UserId = userId,
			Request = input,
			ScopeSnapshot = scopeSnapshot,
			GenerationId = generationId
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

	public async Task<List<HrReportExportJobDto>> GetExportsAsync(HrReportExportListRequestDto input)
	{
		var userId = GetRequiredUserId();
		return await jobAccessService.GetRecentJobsAsync(userId, Clock.Now, input);
	}

	public async Task<HrReportExportJobDto> GetExportAsync(Guid jobId) =>
		HrReportProjection.ToJobDto(await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId()), Clock.Now);

	public async Task<HrReportExportJobDto> CancelExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetOwnedJobAsync(jobId, GetRequiredUserId());
		EnsureExportStatus(job, "cancel", HrReportExportStatus.Queued, HrReportExportStatus.Running);
		job.MarkCancelled(Clock.Now);
		await exportJobRepository.UpdateAsync(job, autoSave: true);
		await WriteAuditAsync(job.Id, "export_cancelled", $"Cancelled HR report export {job.ReportType}", new { job.ReportType, job.Format });

		return HrReportProjection.ToJobDto(job, Clock.Now);
	}

	public async Task DeleteFailedExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetOwnedJobAsync(jobId, GetRequiredUserId());
		EnsureExportStatus(job, "delete", HrReportExportStatus.Failed);
		await exportJobRepository.DeleteAsync(job, autoSave: true);
		await WriteAuditAsync(
			job.Id,
			"export_deleted",
			$"Deleted failed HR report export {job.ReportType}",
			new { job.ReportType, job.Format, job.Status });
	}

	public async Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetOwnedJobAsync(jobId, GetRequiredUserId());
		EnsureExportStatus(job, "retry", HrReportExportStatus.Failed, HrReportExportStatus.Cancelled);
		var request = DeserializeRequest(job);
		request.SourceAction = "retry";
		if (PayrollIssuedReportBoundary.IsSalaryDocument(request.DataSource)) request.Filters.IncludeDrafts = false;
		request.ParentJobId = job.Id;
		var scopeSnapshot = await scopeAuthorizationService.CaptureAsync(
			job.RequestedByUserId,
			job.TenantId);
		await recipientService.ValidateAsync(
			job.GetSharedUserIds(),
			scopeSnapshot);
		var generationId = guidGenerator.Create();
		job.MarkQueued(Clock.Now.AddDays(7));
		job.UpdateRequest(
			JsonSerializer.Serialize(request.Filters ?? new HrReportFilterDto(), JsonOptions),
			HrReportStoredExportRequestSerializer.Serialize(request, scopeSnapshot, generationId));
		await exportJobRepository.UpdateAsync(job, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new HrReportExportJobArgs
		{
			JobId = job.Id,
			TenantId = job.TenantId,
			UserId = job.RequestedByUserId,
			Request = request,
			ScopeSnapshot = scopeSnapshot,
			GenerationId = generationId
		});
		await WriteAuditAsync(job.Id, "export_retried", $"Retried HR report export {job.ReportType}", new { job.ReportType, job.Format });

		return new HrReportExportCreateResponseDto { JobId = job.Id, Status = job.Status };
	}

	public async Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId());
		EnsureCanRegenerate(job);
		var request = DeserializeRequest(job);
		request.SourceAction = "regenerate";
		request.ParentJobId = job.Id;
		var created = await CreateExportForUserAsync(
			request,
			GetRequiredUserId(),
			CurrentTenant.Id,
			sourceAction: "regenerate",
			parentJobId: job.Id);
		await WriteAuditAsync(job.Id, "export_regenerated", $"Regenerated HR report export {job.ReportType}", new { OriginalJobId = job.Id, created.JobId });
		return created;
	}

	public async Task<HrReportExportJobDto> ShareExportAsync(Guid jobId, HrReportShareExportRequestDto input)
	{
		var ownerUserId = GetRequiredUserId();
		var job = await jobAccessService.GetOwnedJobAsync(jobId, ownerUserId);
		var userIds = await recipientService.ResolveShareRecipientsAsync(
			job,
			ownerUserId,
			input.UserIds);
		job.ShareWith(userIds);
		await exportJobRepository.UpdateAsync(job, autoSave: true);
		await WriteAuditAsync(job.Id, "export_shared", $"Shared HR report export {job.ReportType}", new { job.ReportType, SharedUserIds = userIds });
		return HrReportProjection.ToJobDto(job, Clock.Now);
	}

	public async Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId)
	{
		var job = await jobAccessService.GetAuthorizedJobAsync(jobId, GetRequiredUserId());
		PayrollIssuedReportBoundary.EnsureLegacyExport(DeserializeRequest(job));
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
		try
		{
			var request = HrReportStoredExportRequestSerializer.DeserializeRequest(job.RequestJson);
			if (request != null)
			{
				return request;
			}
		}
		catch (JsonException)
		{
			// Fall back to the earlier filter payload so retry/regenerate still has a usable request.
		}

		var filters = JsonSerializer.Deserialize<HrReportFilterDto>(job.FilterJson, JsonOptions) ?? new HrReportFilterDto();
		return new HrReportRequestDto
		{
			ReportType = job.ReportType,
			Format = job.Format,
			Filters = filters
		};
	}

	private static void EnsureExportStatus(HrReportExportJob job, string action, params string[] allowedStatuses)
	{
		if (!allowedStatuses.Contains(job.Status, StringComparer.Ordinal))
		{
			throw new BusinessException(HRErrorCodes.ReportExportNotReady)
				.WithData("action", action)
				.WithData("status", job.Status);
		}
	}

	private void EnsureCanRegenerate(HrReportExportJob job)
	{
		if (job.Status != HrReportExportStatus.Completed || job.ExpiresAt > Clock.Now)
		{
			throw new BusinessException(HRErrorCodes.ReportExportNotReady)
				.WithData("action", "regenerate")
				.WithData("status", job.Status);
		}
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
