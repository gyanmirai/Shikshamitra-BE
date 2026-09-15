using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Library.Reports;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Reports.View)]
[RemoteService(false)]
public class LibraryReportAppService(
	LibraryReportGenerator reportGenerator,
	LibraryReportScopeService reportScopeService,
	ILibraryAiReportPlanner aiReportPlanner,
	IHrReportExportJobRepository jobRepository,
	IBackgroundJobManager backgroundJobManager,
	IBlobContainer blobContainer)
	: ApplicationService, ILibraryReportAppService
{
	public async Task<LibraryReportOptionsDto> GetOptionsAsync()
	{
		var executionScope = await reportScopeService.GetCurrentExecutionScopeAsync();
		return await reportGenerator.GetOptionsAsync(executionScope);
	}

	public async Task<LibraryAiReportPlanResponseDto> CreateAiPlanAsync(LibraryAiReportPlanRequestDto input)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(LibraryErrorCodes.ReportUserRequired);
		return await aiReportPlanner.CreatePlanAsync(input, userId);
	}

	public async Task<LibraryReportPreviewDto> PreviewAsync(LibraryReportRequestDto input)
	{
		var executionScope = await reportScopeService.AuthorizeAndNormalizeAsync(input);
		return await reportGenerator.GenerateAsync(input, executionScope, exportAllRows: false);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Reports.Export)]
	public async Task<LibraryReportExportCreateResponseDto> CreateExportAsync(LibraryReportRequestDto input)
	{
		var executionScope = await reportScopeService.AuthorizeAndNormalizeAsync(input);

		var tenantId = CurrentTenant.Id;
		var userId = CurrentUser.Id ?? throw new BusinessException(code: LibraryErrorCodes.ReportUserRequired, message: "A signed-in user is required to export Library reports.");
		var jobId = GuidGenerator.Create();
		var expiresAt = DateTime.UtcNow.AddDays(7);
		var filterJson = JsonSerializer.Serialize(input.Filters);
		var requestJson = JsonSerializer.Serialize(input);

		var job = new HrReportExportJob(
			jobId,
			tenantId,
			userId,
			input.ReportType,
			NormalizeFormat(input.Format),
			filterJson,
			requestJson,
			expiresAt);

		await jobRepository.InsertAsync(job, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new LibraryReportExportJobArgs
		{
			JobId = jobId,
			TenantId = tenantId,
			UserId = userId,
			Request = input,
			ExecutionScope = executionScope
		});

		return new LibraryReportExportCreateResponseDto
		{
			JobId = job.Id,
			Status = job.Status
		};
	}

	public async Task<List<LibraryReportExportJobDto>> GetExportsAsync()
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(code: LibraryErrorCodes.ReportUserRequired, message: "A signed-in user is required to view Library report exports.");
		var jobs = await jobRepository.GetRecentAuthorizedJobsAsync(userId, 100);
		return jobs
			.Where(job => LibraryReportCatalog.IsSupported(job.ReportType))
			.Select(MapJob)
			.ToList();
	}

	public async Task<LibraryReportExportJobDto> GetExportAsync(Guid jobId)
	{
		var job = await GetAuthorizedLibraryJobAsync(jobId);
		return MapJob(job);
	}

	public async Task<LibraryReportDownloadDto> DownloadExportAsync(Guid jobId)
	{
		var job = await GetAuthorizedLibraryJobAsync(jobId);
		if (job.Status != HrReportExportStatus.Completed || string.IsNullOrWhiteSpace(job.BlobName))
		{
			throw new BusinessException(code: LibraryErrorCodes.ReportExportNotReady, message: "Library report export is not ready for download.")
				.WithData("Status", job.Status);
		}

		if (DateTime.UtcNow > job.ExpiresAt)
		{
			throw new BusinessException(code: LibraryErrorCodes.ReportExportExpired, message: "Library report export has expired.");
		}

		var bytes = await blobContainer.GetAllBytesOrNullAsync(job.BlobName);
		if (bytes == null)
		{
			throw new BusinessException(code: LibraryErrorCodes.ReportExportFileNotFound, message: "Library report export file was not found.");
		}

		return new LibraryReportDownloadDto
		{
			Bytes = bytes,
			ContentType = job.Format.Equals(LibraryReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
				? "application/pdf"
				: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			FileName = BuildFileName(job)
		};
	}

	private async Task<HrReportExportJob> GetAuthorizedLibraryJobAsync(Guid jobId)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException(code: LibraryErrorCodes.ReportUserRequired, message: "A signed-in user is required to access Library report exports.");
		var job = await jobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId && !job.GetSharedUserIds().Contains(userId))
		{
			throw new UnauthorizedAccessException("You are not authorized to access this Library report export.");
		}

		LibraryReportCatalog.Resolve(job.ReportType);
		return job;
	}

	private static LibraryReportExportJobDto MapJob(HrReportExportJob job) =>
		new()
		{
			Id = job.Id,
			ReportType = job.ReportType,
			Format = job.Format,
			Status = job.Status,
			RequestSnapshot = DeserializeRequest(job.RequestJson),
			FileName = job.Status == HrReportExportStatus.Completed ? BuildFileName(job) : null,
			ErrorMessage = job.ErrorMessage,
			ProgressPercent = job.ProgressPercent,
			RowCount = job.RowCount,
			FileSizeBytes = job.FileSizeBytes,
			CreationTime = job.CreationTime,
			StartedAt = job.StartedAt,
			CompletedAt = job.CompletedAt,
			ExpiresAt = job.ExpiresAt
		};

	private static LibraryReportRequestDto? DeserializeRequest(string requestJson)
	{
		try
		{
			return JsonSerializer.Deserialize<LibraryReportRequestDto>(requestJson);
		}
		catch
		{
			return null;
		}
	}

	private static string NormalizeFormat(string? format) =>
		string.Equals(format, LibraryReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
			? LibraryReportFormat.Pdf
			: LibraryReportFormat.Excel;

	private static string BuildFileName(HrReportExportJob job)
	{
		var extension = job.Format.Equals(LibraryReportFormat.Pdf, StringComparison.OrdinalIgnoreCase) ? "pdf" : "xlsx";
		var name = job.ReportType.Replace(" ", "_").Replace("/", "_");
		return $"{name}_{job.CreationTime:yyyyMMddHHmmss}.{extension}";
	}
}

public class LibraryReportExportJobArgs
{
	public Guid JobId { get; set; }
	public Guid? TenantId { get; set; }
	public Guid UserId { get; set; }
	public LibraryReportRequestDto Request { get; set; } = null!;
	public LibraryReportExecutionScope ExecutionScope { get; set; } = null!;
}
