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

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportExportBackgroundJob(
    AcademicReportGenerator reportGenerator,
    AcademicPdfService pdfService,
    TimetableReportsPdfService timetablePdfService,
    AcademicExcelService excelService,
    IRepository<ShikshaMitra.HR.Reports.HrReportExportJob, Guid> exportJobRepository,
    IBlobContainer blobContainer,
    ICurrentTenant currentTenant,
    ILogger<AcademicReportExportBackgroundJob> logger)
    : AsyncBackgroundJob<AcademicReportExportJobArgs>, ITransientDependency
{
    [UnitOfWork]
    public override async Task ExecuteAsync(AcademicReportExportJobArgs args)
    {
        using (currentTenant.Change(args.TenantId))
        {
            var job = await exportJobRepository.GetAsync(args.JobId);
            job.MarkRunning(DateTime.UtcNow);
            await exportJobRepository.UpdateAsync(job, autoSave: true);

            try
            {
                var report = await reportGenerator.GenerateAsync(args.Request, args.UserId, args.TenantId, exportAllRows: true);

                var format = string.Equals(args.Request.Format, AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
                    ? AcademicReportFormat.Pdf
                    : AcademicReportFormat.Excel;

                var bytes = format == AcademicReportFormat.Pdf
                    ? TimetableReportsPdfService.IsTimetableReport(report.ReportType)
                        ? await timetablePdfService.GenerateAsync(report)
                        : await pdfService.GenerateReportPdf(report)
                    : excelService.GenerateReportExcel(report);

                var blobName = BuildBlobName(args, format);

                await blobContainer.SaveAsync(blobName, bytes, overrideExisting: true);

                job.MarkCompleted(blobName, DateTime.UtcNow);
                await exportJobRepository.UpdateAsync(job, autoSave: true);

                logger.LogInformation("Successfully completed Academic report export {JobId}", args.JobId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute Academic report export {JobId}", args.JobId);
                job.MarkFailed(ex.Message, DateTime.UtcNow);
                await exportJobRepository.UpdateAsync(job, autoSave: true);
            }
        }
    }

    private static string BuildBlobName(AcademicReportExportJobArgs args, string format)
    {
        var extension = format == AcademicReportFormat.Pdf ? "pdf" : "xlsx";
        return $"academic-exports/{args.TenantId}/{args.UserId}/{args.JobId}.{extension}";
    }
}
