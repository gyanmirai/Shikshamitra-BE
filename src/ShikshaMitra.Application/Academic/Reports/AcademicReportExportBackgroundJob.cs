using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
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
    AcademicDynamicReportQueryService dynamicReportQueryService,
    AcademicDynamicReportPdfService dynamicReportPdfService,
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
            if (job.Status == HrReportExportStatus.Cancelled)
            {
                logger.LogInformation("Skipping cancelled Academic report export {JobId}", args.JobId);
                return;
            }

            job.MarkRunning(DateTime.UtcNow);
            await exportJobRepository.UpdateAsync(job, autoSave: true);

            try
            {
                AcademicReportPreviewDto report;
                string format;
                byte[] bytes;
                if (args.DynamicSpec != null)
                {
                    var scope = new AcademicAiReportCatalogScope(
                        args.AcademicYearId,
                        args.EducationBoardId,
                        args.EducationMediumId,
                        args.AllowedClassIds,
                        args.AllowedDivisionIds);
                    report = await dynamicReportQueryService.GenerateAsync(args.DynamicSpec, scope);
                    format = AcademicReportFormat.Pdf;
                    bytes = dynamicReportPdfService.Generate(args.DynamicSpec, report);
                }
                else
                {
                    var request = args.Request ?? throw new BusinessException("Academic:Reports:RequestRequired");
                    args.Request = reportGenerator.NormalizeAndValidateRequest(request, exportAllRows: true);
                    report = await reportGenerator.GenerateAsync(args.Request, args.UserId, args.TenantId, exportAllRows: true);
                    format = string.Equals(args.Request.Format, AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
                        ? AcademicReportFormat.Pdf
                        : AcademicReportFormat.Excel;
                    bytes = format == AcademicReportFormat.Pdf
                        ? TimetableReportsPdfService.IsTimetableReport(report.ReportType)
                            ? await timetablePdfService.GenerateAsync(report)
                            : await pdfService.GenerateReportPdf(report)
                        : excelService.GenerateReportExcel(report);
                }

                job = await exportJobRepository.GetAsync(args.JobId);
                if (job.Status == HrReportExportStatus.Cancelled)
                {
                    logger.LogInformation("Academic report export {JobId} was cancelled before file save", args.JobId);
                    return;
                }

                var blobName = BuildBlobName(args, format);

                await blobContainer.SaveAsync(blobName, bytes, overrideExisting: true);

                job = await exportJobRepository.GetAsync(args.JobId);
                if (job.Status == HrReportExportStatus.Cancelled)
                {
                    logger.LogInformation("Academic report export {JobId} was cancelled before completion update", args.JobId);
                    return;
                }

                job.MarkCompleted(blobName, DateTime.UtcNow, report.TotalCount, bytes.LongLength);
                await exportJobRepository.UpdateAsync(job, autoSave: true);

                logger.LogInformation("Successfully completed Academic report export {JobId}", args.JobId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute Academic report export {JobId}", args.JobId);
                job = await exportJobRepository.GetAsync(args.JobId);
                if (job.Status != HrReportExportStatus.Cancelled)
                {
                    job.MarkFailed(ex.Message, DateTime.UtcNow);
                    await exportJobRepository.UpdateAsync(job, autoSave: true);
                }
            }
        }
    }

    private static string BuildBlobName(AcademicReportExportJobArgs args, string format)
    {
        var extension = format == AcademicReportFormat.Pdf ? "pdf" : "xlsx";
        return $"academic-exports/{args.TenantId}/{args.UserId}/{args.JobId}.{extension}";
    }
}
