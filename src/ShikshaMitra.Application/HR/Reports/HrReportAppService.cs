using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.View)]
public class HrReportAppService(
	HrReportGenerator reportGenerator,
	IRepository<HrReportExportJob, Guid> exportJobRepository,
	IBackgroundJobManager backgroundJobManager,
	IBlobContainer blobContainer,
	IGuidGenerator guidGenerator) : ApplicationService, IHrReportAppService
{
	public async Task<HrReportOptionsDto> GetOptionsAsync()
	{
		var userId = CurrentUser.Id ?? throw new BusinessException("HR:Reports:050003");
		return await reportGenerator.GetOptionsAsync(userId, CurrentTenant.Id);
	}

	public async Task<HrReportPreviewDto> PreviewAsync(HrReportRequestDto input)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException("HR:Reports:050003");
		return await reportGenerator.GenerateAsync(input, userId, CurrentTenant.Id, exportAllRows: false);
	}

	public async Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException("HR:Reports:050003");
		var job = new HrReportExportJob(
			guidGenerator.Create(),
			CurrentTenant.Id,
			userId,
			input.ReportType,
			NormalizeFormat(input.Format),
			JsonSerializer.Serialize(input.Filters ?? new HrReportFilterDto()),
			Clock.Now.AddDays(7));

		await exportJobRepository.InsertAsync(job, autoSave: true);
		await backgroundJobManager.EnqueueAsync(new HrReportExportJobArgs
		{
			JobId = job.Id,
			TenantId = CurrentTenant.Id,
			UserId = userId,
			Request = input
		});

		return new HrReportExportCreateResponseDto
		{
			JobId = job.Id,
			Status = job.Status
		};
	}

	public async Task<List<HrReportExportJobDto>> GetExportsAsync()
	{
		var userId = CurrentUser.Id ?? throw new BusinessException("HR:Reports:050003");
		var query = await exportJobRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			query = query.Where(x => x.TenantId == CurrentTenant.Id);
		}

		return (await AsyncExecuter.ToListAsync(
				query
					.Where(x => x.RequestedByUserId == userId)
					.OrderByDescending(x => x.CreationTime)
					.Take(50)))
			.Select(MapJob)
			.ToList();
	}

	public async Task<HrReportExportJobDto> GetExportAsync(Guid jobId)
	{
		return MapJob(await GetAuthorizedJobAsync(jobId));
	}

	public async Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId)
	{
		var job = await GetAuthorizedJobAsync(jobId);
		if (job.Status != HrReportExportStatus.Completed || string.IsNullOrWhiteSpace(job.BlobName))
		{
			throw new BusinessException("HR:Reports:050004")
				.WithData("status", job.Status);
		}

		await using var stream = await blobContainer.GetAsync(job.BlobName);
		await using var memory = new MemoryStream();
		await stream.CopyToAsync(memory);
		var format = NormalizeFormat(job.Format);
		return new HrReportDownloadDto
		{
			Bytes = memory.ToArray(),
			ContentType = format == HrReportFormat.Pdf
				? "application/pdf"
				: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			FileName = BuildFileName(job.ReportType, format)
		};
	}

	private async Task<HrReportExportJob> GetAuthorizedJobAsync(Guid jobId)
	{
		var userId = CurrentUser.Id ?? throw new BusinessException("HR:Reports:050003");
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId || job.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("HR:Reports:050005");
		}

		return job;
	}

	private static HrReportExportJobDto MapJob(HrReportExportJob job)
	{
		var format = NormalizeFormat(job.Format);
		return new HrReportExportJobDto
		{
			Id = job.Id,
			ReportType = job.ReportType,
			Format = format,
			Status = job.Status,
			FileName = job.Status == HrReportExportStatus.Completed ? BuildFileName(job.ReportType, format) : null,
			ErrorMessage = job.ErrorMessage,
			CreationTime = job.CreationTime,
			StartedAt = job.StartedAt,
			CompletedAt = job.CompletedAt,
			ExpiresAt = job.ExpiresAt
		};
	}

	private static string NormalizeFormat(string? format) =>
		string.Equals(format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
			? HrReportFormat.Pdf
			: HrReportFormat.Excel;

	private static string BuildFileName(string reportType, string format)
	{
		var safeName = string.Join("-", reportType.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
			.Replace(' ', '-')
			.ToLowerInvariant();
		return $"{safeName}.{(format == HrReportFormat.Pdf ? "pdf" : "xlsx")}";
	}
}

public class HrReportExportJobArgs
{
	public Guid JobId { get; set; }
	public Guid? TenantId { get; set; }
	public Guid UserId { get; set; }
	public HrReportRequestDto Request { get; set; } = new();
}
