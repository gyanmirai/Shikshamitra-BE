using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportExportBackgroundJob(
    FinanceReportGenerator reportGenerator,
    FinancePdfService pdfService,
    FinanceExcelService excelService,
    IHrReportExportJobRepository jobRepository,
    IBlobContainer blobContainer,
    ICurrentTenant currentTenant,
    ILogger<FinanceReportExportBackgroundJob> logger)
    : AsyncBackgroundJob<FinanceReportExportJobArgs>, ITransientDependency
{
    [UnitOfWork]
    public override async Task ExecuteAsync(FinanceReportExportJobArgs args)
    {
        using (currentTenant.Change(args.TenantId))
        {
            var job = await jobRepository.GetAsync(args.JobId);
            job.MarkRunning(DateTime.UtcNow);
            await jobRepository.UpdateAsync(job);

        try
        {
            var data = await reportGenerator.GenerateAsync(args.Request, exportAllRows: true);

            byte[] fileBytes;
            if (args.Request.Format.ToLower() == "pdf")
            {
                fileBytes = await pdfService.GenerateReportPdfAsync(data);
            }
            else
            {
                fileBytes = await excelService.GenerateReportExcelAsync(data);
            }

            var blobName = BuildBlobName(args, args.Request.Format.ToLower());
            await blobContainer.SaveAsync(blobName, fileBytes, overrideExisting: true);

            job.MarkCompleted(blobName, DateTime.UtcNow, data.Rows.Count, fileBytes.Length);
            await jobRepository.UpdateAsync(job);
        }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to export finance report {JobId}", args.JobId);
                job.MarkFailed(ex.Message, DateTime.UtcNow);
                await jobRepository.UpdateAsync(job);
            }
        }
    }

    private static string BuildBlobName(FinanceReportExportJobArgs args, string format)
    {
        var ext = format == "pdf" ? "pdf" : "xlsx";
        return $"finance-exports/{args.TenantId}/{args.UserId}/{args.JobId}.{ext}";
    }
}
