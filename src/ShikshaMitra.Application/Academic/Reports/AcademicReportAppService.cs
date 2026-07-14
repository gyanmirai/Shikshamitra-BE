using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Academic.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_SubjectAssignment.View)] // Temp permission fallback
public class AcademicReportAppService(
    AcademicReportGenerator reportGenerator,
    IRepository<ShikshaMitra.HR.Reports.HrReportExportJob, Guid> exportJobRepository,
    IBackgroundJobManager backgroundJobManager,
    IBlobContainer blobContainer,
    IGuidGenerator guidGenerator) : ApplicationService, IAcademicReportAppService
{
    public async Task<AcademicReportOptionsDto> GetOptionsAsync()
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        return await reportGenerator.GetOptionsAsync(userId, CurrentTenant.Id);
    }

    public async Task<AcademicReportPreviewDto> PreviewAsync(AcademicReportRequestDto input)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        input = reportGenerator.NormalizeAndValidateRequest(input, exportAllRows: false);
        return await reportGenerator.GenerateAsync(input, userId, CurrentTenant.Id, exportAllRows: false);
    }

    public async Task<AcademicReportExportCreateResponseDto> CreateExportAsync(AcademicReportRequestDto input)
    {
        var tenantId = CurrentTenant.Id;
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        input = reportGenerator.NormalizeAndValidateRequest(input, exportAllRows: true);
        var jobId = guidGenerator.Create();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var filterJson = JsonSerializer.Serialize(input.Filters);
        var requestJson = JsonSerializer.Serialize(input);

        var job = new ShikshaMitra.HR.Reports.HrReportExportJob(
            jobId,
            tenantId,
            userId,
            input.ReportType,
            input.Format,
            filterJson,
            requestJson,
            expiresAt
        );

        await exportJobRepository.InsertAsync(job, autoSave: true);

        await backgroundJobManager.EnqueueAsync(new AcademicReportExportJobArgs
        {
            JobId = jobId,
            TenantId = tenantId,
            UserId = userId,
            Request = input
        });

        return new AcademicReportExportCreateResponseDto
        {
            JobId = jobId,
            Status = "Queued"
        };
    }

    public async Task<List<AcademicReportExportJobDto>> GetExportsAsync()
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");

        var jobs = await exportJobRepository.GetListAsync(x => x.RequestedByUserId == userId);

        return jobs
            .OrderByDescending(x => x.CreationTime)
            .Take(100)
            .Select(job => MapJob(job, includeRequestJson: false))
            .ToList();
    }

    public async Task<AcademicReportExportJobDto> GetExportAsync(Guid jobId)
    {
        var job = await GetAuthorizedJobAsync(jobId);
        return MapJob(job, includeRequestJson: true);
    }

    public async Task<AcademicReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        var job = await GetAuthorizedJobAsync(jobId);

        if (job.Status != HrReportExportStatus.Completed || string.IsNullOrEmpty(job.BlobName))
        {
            throw new BusinessException("Academic:Reports:050001").WithData("Status", job.Status); // Not Ready
        }

        if (DateTime.UtcNow > job.ExpiresAt)
        {
            throw new BusinessException("Academic:Reports:050002"); // Expired
        }

        var bytes = await blobContainer.GetAllBytesOrNullAsync(job.BlobName);
        if (bytes == null)
        {
            throw new BusinessException("Academic:Reports:050004"); // File not found in blob
        }

        var contentType = job.Format.Equals(AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        var extension = job.Format.Equals(AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase) ? "pdf" : "xlsx";
        var fileName = BuildDownloadFileName(job.ReportType, job.CreationTime, extension);

        return new AcademicReportDownloadDto
        {
            Bytes = bytes,
            ContentType = contentType,
            FileName = fileName
        };
    }

    private async Task<ShikshaMitra.HR.Reports.HrReportExportJob> GetAuthorizedJobAsync(Guid jobId)
    {
        var userId = CurrentUser.Id ?? throw new BusinessException("Academic:Reports:050003");
        var job = await exportJobRepository.GetAsync(jobId);

        if (job.RequestedByUserId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this export job.");
        }

        return job;
    }

    private static AcademicReportExportJobDto MapJob(ShikshaMitra.HR.Reports.HrReportExportJob job, bool includeRequestJson = true)
    {
        var format = job.Format ?? "excel";
        var extension = format.Equals(AcademicReportFormat.Pdf, StringComparison.OrdinalIgnoreCase) ? "pdf" : "xlsx";

        return new AcademicReportExportJobDto
        {
            Id = job.Id,
            ReportType = job.ReportType,
            Format = job.Format ?? "N/A",
            Status = job.Status,
            FileName = job.Status == HrReportExportStatus.Completed
                       ? BuildDownloadFileName(job.ReportType, job.CreationTime, extension)
                       : null,
            ErrorMessage = job.ErrorMessage,
            CreationTime = job.CreationTime,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ExpiresAt = job.ExpiresAt,
            RequestJson = includeRequestJson ? job.RequestJson : null
        };
    }

    private static string BuildDownloadFileName(string reportType, DateTime creationTime, string extension)
    {
        var safeName = new string((reportType ?? "Academic_Report")
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());
        safeName = string.Join("_", safeName.Split('_', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "Academic_Report";
        }

        return $"{safeName}_{creationTime:yyyyMMddHHmmss}.{extension}";
    }
}

public class AcademicReportExportJobArgs
{
    public Guid JobId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid UserId { get; set; }
    public AcademicReportRequestDto Request { get; set; } = null!;
}
