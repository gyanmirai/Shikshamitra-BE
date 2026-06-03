using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
	IGuidGenerator guidGenerator) : ApplicationService, ITransientDependency
{
	public async Task<HrReportExportCreateResponseDto> CreateExportAsync(HrReportRequestDto input)
	{
		var userId = GetRequiredUserId();
		var job = new HrReportExportJob(
			guidGenerator.Create(),
			CurrentTenant.Id,
			userId,
			input.ReportType,
			HrReportProjection.NormalizeFormat(input.Format),
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
		var userId = GetRequiredUserId();
		var query = await exportJobRepository.GetQueryableAsync();

		return (await AsyncExecuter.ToListAsync(
				query
					.Where(x => x.RequestedByUserId == userId)
					.OrderByDescending(x => x.CreationTime)
					.Take(50)))
			.Select(HrReportProjection.ToJobDto)
			.ToList();
	}

	public async Task<HrReportExportJobDto> GetExportAsync(Guid jobId) =>
		HrReportProjection.ToJobDto(await GetAuthorizedJobAsync(jobId));

	public async Task<HrReportDownloadDto> DownloadExportAsync(Guid jobId)
	{
		var job = await GetAuthorizedJobAsync(jobId);
		if (job.Status != HrReportExportStatus.Completed || string.IsNullOrWhiteSpace(job.BlobName))
		{
			throw new BusinessException(HRErrorCodes.ReportExportNotReady)
				.WithData("status", job.Status);
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

	private async Task<HrReportExportJob> GetAuthorizedJobAsync(Guid jobId)
	{
		var userId = GetRequiredUserId();
		var job = await exportJobRepository.GetAsync(jobId);
		if (job.RequestedByUserId != userId)
		{
			throw new BusinessException(HRErrorCodes.ReportAccessDenied);
		}

		return job;
	}

	private Guid GetRequiredUserId() =>
		CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.ReportUserRequired);
}
