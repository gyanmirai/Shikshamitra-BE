using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportBackgroundJob(
	HrReportGenerator reportGenerator,
	HRPdfService pdfService,
	HRExcelService excelService,
	IRepository<HrReportExportJob, Guid> exportJobRepository,
	IBlobContainer blobContainer,
	ICurrentTenant currentTenant,
	IClock clock,
	IUnitOfWorkManager unitOfWorkManager,
	ILogger<HrReportExportBackgroundJob> logger)
	: AsyncBackgroundJob<HrReportExportJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(HrReportExportJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			if (!await TryMarkRunningAsync(args.JobId))
			{
				return;
			}

			try
			{
				var report = await GenerateReportAsync(args);
				var format = string.Equals(args.Request.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
					? HrReportFormat.Pdf
					: HrReportFormat.Excel;
				var bytes = format == HrReportFormat.Pdf
					? await pdfService.GenerateReportPdf(report)
					: excelService.GenerateReportExcel(report);
				var blobName = BuildBlobName(args, format);
				if (await IsCancelledAsync(args.JobId))
				{
					return;
				}

				await using var stream = new MemoryStream(bytes);
				await blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
				if (!await TryMarkCompletedAsync(args.JobId, blobName, report.TotalCount, bytes.LongLength))
				{
					await DeleteBlobBestEffortAsync(blobName);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to export HR report job {JobId}", args.JobId);
				await TryMarkFailedBestEffortAsync(args.JobId, ex.Message);
			}
		}
	}

	private static string BuildBlobName(HrReportExportJobArgs args, string format)
	{
		var extension = format == HrReportFormat.Pdf ? "pdf" : "xlsx";
		var tenant = args.TenantId?.ToString("N") ?? "host";
		return $"hr-report-exports/{tenant}/{args.UserId:N}/{args.JobId:N}.{extension}";
	}

	private async Task<HrReportPreviewDto> GenerateReportAsync(HrReportExportJobArgs args)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var report = await reportGenerator.GenerateAsync(args.Request, args.UserId, args.TenantId, exportAllRows: true);
		await uow.CompleteAsync();
		return report;
	}

	private async Task<bool> TryMarkRunningAsync(Guid jobId)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		if (snapshot.Status == HrReportExportStatus.Cancelled)
		{
			await uow.CompleteAsync();
			return false;
		}

		snapshot.MarkRunning(clock.Now);
		await exportJobRepository.UpdateAsync(snapshot, autoSave: false);
		await uow.CompleteAsync();
		return true;
	}

	private async Task<bool> IsCancelledAsync(Guid jobId)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		await uow.CompleteAsync();
		return snapshot.Status == HrReportExportStatus.Cancelled;
	}

	private async Task<bool> TryMarkCompletedAsync(Guid jobId, string blobName, int rowCount, long fileSizeBytes)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		if (snapshot.Status == HrReportExportStatus.Cancelled)
		{
			await uow.CompleteAsync();
			return false;
		}

		snapshot.MarkCompleted(blobName, clock.Now, rowCount, fileSizeBytes);
		await exportJobRepository.UpdateAsync(snapshot, autoSave: false);
		await uow.CompleteAsync();
		return true;
	}

	private async Task TryMarkFailedBestEffortAsync(Guid jobId, string errorMessage)
	{
		try
		{
			await TryMarkFailedAsync(jobId, errorMessage);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to mark HR report export job {JobId} as failed", jobId);
		}
	}

	private async Task<bool> TryMarkFailedAsync(Guid jobId, string errorMessage)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
		var snapshot = await exportJobRepository.GetAsync(jobId);
		if (snapshot.Status == HrReportExportStatus.Cancelled)
		{
			await uow.CompleteAsync();
			return false;
		}

		snapshot.MarkFailed(errorMessage, clock.Now);
		await exportJobRepository.UpdateAsync(snapshot, autoSave: false);
		await uow.CompleteAsync();
		return true;
	}

	private async Task DeleteBlobBestEffortAsync(string blobName)
	{
		try
		{
			await blobContainer.DeleteAsync(blobName);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to delete cancelled HR report export blob {BlobName}", blobName);
		}
	}
}
