using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Reports;

public class HrReportExportBackgroundJob(
	HrReportGenerator reportGenerator,
	HRPdfService pdfService,
	HRExcelService excelService,
	IRepository<HrReportExportJob, Guid> exportJobRepository,
	IBlobContainer blobContainer,
	ICurrentTenant currentTenant,
	ILogger<HrReportExportBackgroundJob> logger)
	: AsyncBackgroundJob<HrReportExportJobArgs>, ITransientDependency
{
	[UnitOfWork]
	public override async Task ExecuteAsync(HrReportExportJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			var job = await exportJobRepository.GetAsync(args.JobId);
			job.MarkRunning(DateTime.UtcNow);
			await exportJobRepository.UpdateAsync(job, autoSave: true);

			try
			{
				var report = await reportGenerator.GenerateAsync(args.Request, args.UserId, args.TenantId, exportAllRows: true);
				var format = string.Equals(args.Request.Format, HrReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
					? HrReportFormat.Pdf
					: HrReportFormat.Excel;
				var bytes = format == HrReportFormat.Pdf
					? await pdfService.GenerateReportPdf(report)
					: excelService.GenerateReportExcel(report);
				var blobName = BuildBlobName(args, format);

				await using var stream = new MemoryStream(bytes);
				await blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
				job.MarkCompleted(blobName, DateTime.UtcNow);
				await exportJobRepository.UpdateAsync(job, autoSave: true);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to export HR report job {JobId}", args.JobId);
				job.MarkFailed(ex.Message, DateTime.UtcNow);
				await exportJobRepository.UpdateAsync(job, autoSave: true);
			}
		}
	}

	private static string BuildBlobName(HrReportExportJobArgs args, string format)
	{
		var extension = format == HrReportFormat.Pdf ? "pdf" : "xlsx";
		var tenant = args.TenantId?.ToString("N") ?? "host";
		return $"hr-report-exports/{tenant}/{args.UserId:N}/{args.JobId:N}.{extension}";
	}
}
