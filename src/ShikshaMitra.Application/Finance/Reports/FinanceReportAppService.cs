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
    IBackgroundJobManager backgroundJobManager,
    IHrReportExportJobRepository jobRepository,
    IBlobContainer blobContainer)
    : ApplicationService, IFinanceReportAppService
{
    public async Task<FinanceReportOptionsDto> GetOptionsAsync()
    {
        return await reportGenerator.GetOptionsAsync();
    }

    public async Task<FinanceReportPreviewDto> PreviewAsync(FinanceReportRequestDto input)
    {
        return await reportGenerator.GenerateAsync(input, exportAllRows: false);
    }

    public async Task<FinanceReportExportCreateResponseDto> CreateExportAsync(FinanceReportRequestDto input)
    {
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
            Request = input
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
            throw new UnauthorizedAccessException();
        }

        if (string.IsNullOrEmpty(job.BlobName))
        {
            throw new Exception("Report file not generated yet.");
        }

        var bytes = await blobContainer.GetAllBytesOrNullAsync(job.BlobName);
        if (bytes == null)
        {
            throw new Exception("Report file not found.");
        }

        return new FinanceReportDownloadDto
        {
            Bytes = bytes,
            FileName = job.BlobName.Split('/').Last(),
            ContentType = job.Format!.ToLower() == "pdf" 
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
}
