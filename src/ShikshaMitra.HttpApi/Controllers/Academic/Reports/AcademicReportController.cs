using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Academic.Reports;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/academic-report")]
public class AcademicReportController(
    IAcademicReportAppService reportAppService) : AbpControllerBase, IAcademicReportAppService
{
    [HttpGet("options")]
    public virtual Task<AcademicReportOptionsDto> GetOptionsAsync()
    {
        return reportAppService.GetOptionsAsync();
    }

    [HttpPost("preview")]
    public virtual Task<AcademicReportPreviewDto> PreviewAsync(AcademicReportRequestDto input)
    {
        return reportAppService.PreviewAsync(input);
    }

    [HttpPost("exports")]
    public virtual Task<AcademicReportExportCreateResponseDto> CreateExportAsync(AcademicReportRequestDto input)
    {
        return reportAppService.CreateExportAsync(input);
    }

    [HttpGet("exports")]
    public virtual Task<List<AcademicReportExportJobDto>> GetExportsAsync()
    {
        return reportAppService.GetExportsAsync();
    }

    [HttpGet("exports/{jobId}")]
    public virtual Task<AcademicReportExportJobDto> GetExportAsync(Guid jobId)
    {
        return reportAppService.GetExportAsync(jobId);
    }

    [HttpGet("exports/{jobId}/download")]
    public virtual async Task<AcademicReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        return await reportAppService.DownloadExportAsync(jobId);
    }
}
