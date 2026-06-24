using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Finance.Reports;

[RemoteService(Name = "Default")]
[Area("finance")]
[Route("api/app/finance-report")]
public class FinanceReportController(IFinanceReportAppService appService) : AbpController, IFinanceReportAppService
{
    [HttpGet("options")]
    public Task<FinanceReportOptionsDto> GetOptionsAsync()
    {
        return appService.GetOptionsAsync();
    }

    [HttpPost("preview")]
    public Task<FinanceReportPreviewDto> PreviewAsync(FinanceReportRequestDto input)
    {
        return appService.PreviewAsync(input);
    }

    [HttpPost("exports")]
    public Task<FinanceReportExportCreateResponseDto> CreateExportAsync(FinanceReportRequestDto input)
    {
        return appService.CreateExportAsync(input);
    }

    [HttpGet("exports/{jobId}/download")]
    public async Task<IActionResult> DownloadExportFileAsync(Guid jobId)
    {
        var result = await appService.DownloadExportAsync(jobId);
        return File(result.Bytes, result.ContentType, result.FileName);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public Task<FinanceReportDownloadDto> DownloadExportAsync(Guid jobId)
    {
        return appService.DownloadExportAsync(jobId);
    }
}
