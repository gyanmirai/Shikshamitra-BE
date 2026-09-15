using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;
using Volo.Abp;

namespace ShikshaMitra.Finance.Reports;

[Authorize(ShikshaMitraSubModulePermissions.FinanceReports)]
[RemoteService(IsEnabled = false)]
public class FinanceReportAppService(
    FinanceReportGenerator reportGenerator,
    FinanceReportScopeService reportScopeService,
    IFinanceAiReportPlanner aiReportPlanner,
    IBackgroundJobManager backgroundJobManager,
    IHrReportExportJobRepository jobRepository,
    IBlobContainer blobContainer)
    : ApplicationService, IFinanceReportAppService
{
    public async Task<FinanceReportOptionsDto> GetOptionsAsync()
    {
        var allowedAcademicYearIds = await reportScopeService.GetAccessibleAcademicYearIdsAsync();
        return await reportGenerator.GetOptionsAsync(allowedAcademicYearIds);
    }

    public async Task<FinanceAiReportPlanResponseDto> CreateAiPlanAsync(FinanceAiReportPlanRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new UnauthorizedAccessException();
        return await aiReportPlanner.CreatePlanAsync(input, userId);
    }

    public async Task<FinanceReportPreviewDto> PreviewAsync(FinanceReportRequestDto input)
    {
        var executionScope = await reportScopeService.AuthorizeAndNormalizeAsync(input);
        return await reportGenerator.GenerateAsync(input, executionScope, exportAllRows: false);
    }

    public async Task<FinanceReportExportCreateResponseDto> CreateExportAsync(FinanceReportRequestDto input)
    {
        var executionScope = await reportScopeService.AuthorizeAndNormalizeAsync(input);
        var tenantId = CurrentTenant.Id;
        var userId = CurrentUser.Id ?? throw new UnauthorizedAccessException();
        var jobId = GuidGenerator.Create();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var filterJson = System.Text.Json.JsonSerializer.Serialize(input.Filters);
        var requestJson = System.Text.Json.JsonSerializer.Serialize(input);

        var job = new HrReportExportJob(
            jobId,
            tenantId,
            userId,
            input.ReportType,
            input.Format,
            filterJson,
            requestJson,
            expiresAt);

        await jobRepository.InsertAsync(job, autoSave: true);

        await backgroundJobManager.EnqueueAsync(new FinanceReportExportJobArgs
        {
            JobId = jobId,
            TenantId = tenantId,
            UserId = userId,
            Request = input,
            ExecutionScope = executionScope
        }, delay: TimeSpan.FromSeconds(2));

        return new FinanceReportExportCreateResponseDto
        {
            JobId = job.Id,
            Status = job.Status
        };
    }

    public async Task<FinanceReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        var job = await jobRepository.GetAsync(jobId);
        if (job.RequestedByUserId != CurrentUser.Id)
        {
            throw new BusinessException(FinanceReportRequestGuard.AccessDenied);
        }

        if (job.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessException(FinanceReportRequestGuard.ExportExpired);
        }

        if (!string.Equals(job.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrEmpty(job.BlobName))
        {
            throw new BusinessException(FinanceReportRequestGuard.ExportNotReady)
                .WithData("Status", job.Status);
        }

        var bytes = await blobContainer.GetAllBytesOrNullAsync(job.BlobName);
        if (bytes == null)
        {
            throw new BusinessException(FinanceReportRequestGuard.ExportNotFound);
        }

        return new FinanceReportDownloadDto
        {
            Bytes = bytes,
            FileName = job.BlobName.Split('/').Last(),
            ContentType = string.Equals(job.Format, "pdf", StringComparison.OrdinalIgnoreCase)
                ? "application/pdf" 
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }
}

public class FinanceReportExportJobArgs
{
    public Guid JobId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public FinanceReportRequestDto Request { get; set; } = null!;
    public FinanceReportExecutionScope ExecutionScope { get; set; } = null!;
}
