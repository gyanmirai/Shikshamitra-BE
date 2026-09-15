using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportExportBackgroundJob(
	LibraryReportGenerator reportGenerator,
	LibraryPdfService pdfService,
	LibraryExcelService excelService,
	IHrReportExportJobRepository jobRepository,
	IBlobContainer blobContainer,
	ICurrentTenant currentTenant,
	ILogger<LibraryReportExportBackgroundJob> logger)
	: AsyncBackgroundJob<LibraryReportExportJobArgs>, ITransientDependency
{
	[UnitOfWork]
	public override async Task ExecuteAsync(LibraryReportExportJobArgs args)
	{
		using (currentTenant.Change(args.TenantId))
		{
			var job = await jobRepository.GetAsync(args.JobId);
			job.MarkRunning(DateTime.UtcNow);
			await jobRepository.UpdateAsync(job, autoSave: true);

			try
			{
				var report = await reportGenerator.GenerateAsync(args.Request, args.ExecutionScope, exportAllRows: true);
				var format = string.Equals(args.Request.Format, LibraryReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
					? LibraryReportFormat.Pdf
					: LibraryReportFormat.Excel;

				var bytes = format == LibraryReportFormat.Pdf
					? await pdfService.GenerateReportPdfAsync(report)
					: excelService.GenerateReportExcel(report);

				var blobName = BuildBlobName(args, format);
				await blobContainer.SaveAsync(blobName, bytes, overrideExisting: true);

				job.MarkCompleted(blobName, DateTime.UtcNow, report.TotalCount, bytes.LongLength);
				await jobRepository.UpdateAsync(job, autoSave: true);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to execute Library report export {JobId}", args.JobId);
				job.MarkFailed(ex.Message, DateTime.UtcNow);
				await jobRepository.UpdateAsync(job, autoSave: true);
			}
		}
	}

	private static string BuildBlobName(LibraryReportExportJobArgs args, string format)
	{
		var extension = format == LibraryReportFormat.Pdf ? "pdf" : "xlsx";
		return $"library-exports/{args.TenantId}/{args.UserId}/{args.JobId}.{extension}";
	}
}
