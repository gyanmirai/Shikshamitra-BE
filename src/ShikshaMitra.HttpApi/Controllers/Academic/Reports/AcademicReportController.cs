using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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

    [HttpPost("ai/exports")]
    public virtual Task<AcademicAiReportExportResponseDto> CreateAiExportAsync(AcademicAiReportExportRequestDto input)
    {
        return reportAppService.CreateAiExportAsync(input);
    }

    [HttpPost("ai/plan")]
    public virtual Task<AcademicAiReportPlanResponseDto> CreateAiPlanAsync(AcademicAiReportPlanRequestDto input)
    {
        return reportAppService.CreateAiPlanAsync(input);
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
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public virtual async Task<IActionResult> DownloadExportFileAsync(Guid jobId)
    {
        var file = await reportAppService.DownloadExportAsync(jobId);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public virtual Task<AcademicReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        return reportAppService.DownloadExportAsync(jobId);
    }
}
