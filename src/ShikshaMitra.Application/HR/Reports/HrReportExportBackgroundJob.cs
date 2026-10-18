using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportBackgroundJob(
	HrReportGenerator reportGenerator,
	HRPdfService pdfService,
	HRExcelService excelService,
	HrReportExportJobStateService jobStateService,
	IBlobContainer blobContainer,
	ICurrentTenant currentTenant,
	IUnitOfWorkManager unitOfWorkManager,
	HrReportingAccess access,
	ILogger<HrReportExportBackgroundJob> logger)
	: AsyncBackgroundJob<HrReportExportJobArgs>, ITransientDependency
{
	public override async Task ExecuteAsync(HrReportExportJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			if (!await jobStateService.TryMarkRunningAsync(args))
			{
				return;
			}

			HrReportGenerationContext? generationContext = null;
			try
			{
				generationContext = await jobStateService.LoadGenerationContextAsync(args);
				if (generationContext == null)
				{
					return;
				}

				var report = await GenerateReportAsync(generationContext);
				var format = string.Equals(generationContext.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
					? HrReportFormat.Pdf
					: HrReportFormat.Excel;
				var bytes = format == HrReportFormat.Pdf
					? await pdfService.GenerateReportPdf(report)
					: excelService.GenerateReportExcel(report);
				var blobName = BuildBlobName(
					args.JobId,
					generationContext.GenerationId,
					generationContext.TenantId,
					generationContext.UserId,
					format);
				if (!await jobStateService.IsCurrentGenerationRunningAsync(
					args.JobId,
					generationContext.GenerationId))
				{
					return;
				}

				await using var stream = new MemoryStream(bytes);
				await blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
				var exportedRowCount = format == HrReportFormat.Pdf
					? HRPdfService.GetIncludedRowCount(report)
					: report.Rows.Count;
				if (!await jobStateService.TryMarkCompletedAsync(
					args.JobId,
					generationContext.GenerationId,
					blobName,
					exportedRowCount,
					bytes.LongLength))
				{
					await DeleteBlobIfUncommittedBestEffortAsync(
						args.JobId,
						generationContext.GenerationId,
						blobName);
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to export HR report job {JobId}", args.JobId);
				await TryMarkFailedBestEffortAsync(
					args.JobId,
					generationContext?.GenerationId ?? args.GenerationId,
					HrReportExportErrorFormatter.ToPersistedMessage(args.Request.Format));
			}
		}
	}

	internal static string BuildBlobName(
		Guid jobId,
		Guid generationId,
		Guid? tenantId,
		Guid userId,
		string format)
	{
		var extension = format == HrReportFormat.Pdf ? "pdf" : "xlsx";
		var tenant = tenantId?.ToString("N") ?? "host";
		return $"hr-report-exports/{tenant}/{userId:N}/{jobId:N}-{generationId:N}.{extension}";
	}

	private async Task<HrReportPreviewDto> GenerateReportAsync(HrReportGenerationContext context)
	{
		using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
		if (context.Request.DataSource is not ("payroll_register" or "payroll_statement")) throw new Volo.Abp.Authorization.AbpAuthorizationException();
		await access.EnsureAsync(context.Request.DataSource, context.UserId);
		var report = await reportGenerator.GenerateExportAsync(
			context.Request,
			context.UserId,
			context.TenantId,
			context.ScopeSnapshot);
		await uow.CompleteAsync();
		return report;
	}

	private async Task TryMarkFailedBestEffortAsync(
		Guid jobId,
		Guid generationId,
		string errorMessage)
	{
		try
		{
			await jobStateService.TryMarkFailedAsync(jobId, generationId, errorMessage);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to mark HR report export job {JobId} as failed", jobId);
		}
	}

	internal async Task DeleteBlobIfUncommittedBestEffortAsync(
		Guid jobId,
		Guid generationId,
		string blobName)
	{
		try
		{
			if (await jobStateService.IsCompletedBlobAsync(jobId, generationId, blobName))
			{
				return;
			}

			await blobContainer.DeleteAsync(blobName);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to delete uncommitted HR report export blob {BlobName}", blobName);
		}
	}
}