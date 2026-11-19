using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Reports;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportExportBackgroundJob(
    FinanceReportGenerator reportGenerator,
    FinancePdfService pdfService,
    FinanceExcelService excelService,
    IHrReportExportJobRepository jobRepository,
    IBlobContainer blobContainer,
    ICurrentTenant currentTenant,
    IClock clock,
    ILogger<FinanceReportExportBackgroundJob> logger)
    : AsyncBackgroundJob<FinanceReportExportJobArgs>, ITransientDependency
{
    [UnitOfWork]
    public override async Task ExecuteAsync(FinanceReportExportJobArgs args)
    {
        using (currentTenant.Change(args.TenantId))
        {
            var job = await jobRepository.GetAsync(args.JobId);
            DateTime JobTime() => job.ExtraProperties.ContainsKey(FinanceReportAppService.UsesApplicationClock)
                ? clock.Now : DateTime.UtcNow;
            job.MarkRunning(JobTime());
            await jobRepository.UpdateAsync(job);

        try
        {
            var data = await reportGenerator.GenerateAsync(
                args.Request,
                args.ExecutionScope,
                exportAllRows: true);

            byte[] fileBytes;
            if (string.Equals(args.Request.Format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                fileBytes = await pdfService.GenerateReportPdfAsync(data);
            }
            else
            {
                fileBytes = await excelService.GenerateReportExcelAsync(data);
            }

            var blobName = BuildBlobName(args, args.Request.Format.ToLowerInvariant());
            await blobContainer.SaveAsync(blobName, fileBytes, overrideExisting: true);

            job.MarkCompleted(blobName, JobTime(), data.Rows.Count, fileBytes.Length);
            await jobRepository.UpdateAsync(job);
        }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to export finance report {JobId}", args.JobId);
                job.MarkFailed(ex.Message, JobTime());
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
