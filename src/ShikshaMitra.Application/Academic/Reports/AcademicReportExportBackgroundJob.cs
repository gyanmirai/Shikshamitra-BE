using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportExportBackgroundJob(
    AcademicReportGenerator reportGenerator,
    AcademicPdfService pdfService,
    AcademicDynamicReportQueryService dynamicReportQueryService,
    AcademicDynamicReportPdfService dynamicReportPdfService,
    TimetableReportsPdfService timetablePdfService,
    AcademicExcelService excelService,
    AcademicCsvService csvService,
    AcademicReportExportStateService exportState,
    AcademicReportIdentityService schoolIdentity,
    IBlobContainer blobContainer,
    ICurrentTenant currentTenant,
    IUnitOfWorkManager unitOfWorkManager,
    ICancellationTokenProvider cancellationTokenProvider,
    ILogger<AcademicReportExportBackgroundJob> logger)
    : AsyncBackgroundJob<AcademicReportExportJobArgs>, ITransientDependency
{
    [UnitOfWork(IsDisabled = true)]
    public override async Task ExecuteAsync(AcademicReportExportJobArgs args)
    {
        using (currentTenant.Change(args.TenantId))
        {
            var runId = await exportState.TryStartAsync(args.JobId);
            if (!runId.HasValue)
            {
                logger.LogInformation("Skipping Academic report export {JobId} because it is no longer queued", args.JobId);
                return;
            }
            string? pendingBlob = null;
            var hostCancellation = cancellationTokenProvider.Token;
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostCancellation);
            using var leaseCancellation = new CancellationTokenSource();
            var leaseMonitor = MonitorLeaseAsync(args.JobId, runId.Value, runCancellation, leaseCancellation.Token);
            try
            {
                var cancellationToken = runCancellation.Token;
                using var requestCancellation = cancellationTokenProvider.Use(cancellationToken);
                await using var file = AcademicReportExportRows.CreateTemporaryFile();
                string format;
                int rowCount;
                using (var dataUow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
                {
                AcademicReportExportRows export;
                if (args.DynamicSpec != null)
                {
                    var scope = new AcademicAiReportCatalogScope(
                        args.AcademicYearId,
                        args.EducationBoardId,
                        args.EducationMediumId,
                        args.AllowedClassIds,
                        args.AllowedDivisionIds);
                    var report = await dynamicReportQueryService.GenerateAsync(args.DynamicSpec, scope);
                    export = AcademicReportExportRows.FromReport(report);
                    format = args.DynamicSpec.Format;
                    if (format == AcademicReportFormat.Pdf)
                    {
                        await schoolIdentity.PrepareAsync(report, args.EducationBoardId, args.EducationMediumId);
                        dynamicReportPdfService.WriteReportPdf(args.DynamicSpec, report, file);
                    }
                }
                else
                {
                    var request = args.Request ?? throw new BusinessException("Academic:Reports:RequestRequired");
                    args.Request = reportGenerator.NormalizeAndValidateRequest(request, exportAllRows: true);
                    format = string.Equals(args.Request.Format, AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
                        ? AcademicReportFormat.Pdf
                        : AcademicReportFormat.Excel;
                    export = await reportGenerator.GenerateExportAsync(args.Request, args.UserId, args.TenantId, cancellationToken);
                    if (format == AcademicReportFormat.Pdf)
                    {
                        await using var spool = await AcademicReportRowSpool.CreateAsync(
                            WithCheckpoints(export.Rows, args.JobId, runId.Value, cancellationToken), cancellationToken);
                        export.Metadata.TotalCount = spool.Count;
                        await schoolIdentity.PrepareAsync(export.Metadata);
                        // Preserve the existing layout. QuestPDF 2024 still buffers its layout/native
                        // document; the row spool bounds database/source materialization only.
                        if (TimetableReportsPdfService.IsTimetableReport(export.Metadata.ReportType))
                            timetablePdfService.WriteReportPdf(export.Metadata, spool.ReadRows(), spool.Count, file);
                        else
                            pdfService.WriteReportPdf(export.Metadata, spool.ReadRows(), spool.Count, file);
                    }
                }
                var rows = WithCheckpoints(export.Rows, args.JobId, runId.Value, cancellationToken);
                rowCount = format switch
                {
                    AcademicReportFormat.Excel => await excelService.WriteReportExcelAsync(export.Metadata, rows, file, cancellationToken),
                    AcademicReportFormat.Csv => await csvService.WriteReportCsvAsync(export.Metadata, rows, file, cancellationToken),
                    _ => export.Metadata.TotalCount
                };
                await EnsureRunningAsync(args.JobId, runId.Value, cancellationToken);
                await dataUow.CompleteAsync(cancellationToken);
                }
                file.Position = 0;
                // Each run owns its blob, so cleanup cannot remove another retry's successful output.
                pendingBlob = BuildBlobName(args, format, runId.Value);
                using (var blobUow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
                {
                    await blobContainer.SaveAsync(pendingBlob, file, overrideExisting: false, cancellationToken);
                    await blobUow.CompleteAsync(cancellationToken);
                }
                await EnsureRunningAsync(args.JobId, runId.Value, cancellationToken);
                await leaseCancellation.CancelAsync();
                await leaseMonitor;
                if (!await exportState.TryCompleteAsync(args.JobId, runId.Value, pendingBlob, rowCount, file.Length))
                    throw new OperationCanceledException("Academic report export is no longer running.");
                pendingBlob = null;
                logger.LogInformation("Successfully completed Academic report export {JobId}", args.JobId);
            }
            catch (Exception ex)
            {
                await leaseCancellation.CancelAsync();
                await leaseMonitor;
                // Cleanup must survive host shutdown; requeue that interrupted delivery for the job system to retry.
                using var cleanupCancellation = cancellationTokenProvider.Use(CancellationToken.None);
                logger.LogError(ex, "Failed to execute Academic report export {JobId}", args.JobId);
                if (pendingBlob != null)
                {
                    try
                    {
                        if (!await exportState.IsCompletedBlobAsync(args.JobId, runId.Value, pendingBlob))
                        {
                            using var cleanupUow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
                            await blobContainer.DeleteAsync(pendingBlob);
                            await cleanupUow.CompleteAsync();
                        }
                    }
                    catch (Exception cleanupError)
                    {
                        logger.LogError(cleanupError, "Failed to clean incomplete Academic export blob {BlobName}", pendingBlob);
                    }
                }
                if (hostCancellation.IsCancellationRequested)
                {
                    await exportState.TryQueueInterruptedAsync(args.JobId, runId.Value);
                    throw;
                }
                await exportState.TryFailAsync(args.JobId, runId.Value, ex.Message);
            }
            finally
            {
                await leaseCancellation.CancelAsync();
                await leaseMonitor;
            }
        }
    }

    private async IAsyncEnumerable<Dictionary<string, string?>> WithCheckpoints(
        IAsyncEnumerable<Dictionary<string, string?>> rows, Guid jobId, Guid runId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            if (count++ % 256 == 0) await EnsureRunningAsync(jobId, runId, cancellationToken);
            yield return row;
        }
    }

    private async Task MonitorLeaseAsync(Guid jobId, Guid runId, CancellationTokenSource runCancellation,
        CancellationToken stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stop);
                if (!await exportState.RenewLeaseAsync(jobId, runId))
                {
                    await runCancellation.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogWarning(error, "Academic export {JobId} lost its worker lease", jobId);
            await runCancellation.CancelAsync();
        }
    }

    private async Task EnsureRunningAsync(Guid jobId, Guid runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await exportState.IsRunningAsync(jobId, runId))
            throw new OperationCanceledException("Academic report export is no longer running.");
    }

    private static string BuildBlobName(AcademicReportExportJobArgs args, string format, Guid runId)
    {
        var extension = format switch
        {
            AcademicReportFormat.Pdf => "pdf",
            AcademicReportFormat.Csv => "csv",
            _ => "xlsx"
        };
        return $"academic-exports/{args.TenantId}/{args.UserId}/{args.JobId}/{runId:N}.{extension}";
    }
}
