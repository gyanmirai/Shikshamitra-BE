using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Reports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Reports;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/reports")]
[Authorize]
public partial class HrReportController(IHrReportAppService reportAppService) : AbpControllerBase
{
	private IHrReportAppService ReportAppService => reportAppService;

	[HttpPost("preview")]
	public Task<HrReportPreviewDto> PreviewAsync([FromBody] HrReportRequestDto input) => reportAppService.PreviewAsync(input);
	[HttpPost("exports")]
	[ProducesResponseType(typeof(HrReportExportCreateResponseDto), StatusCodes.Status202Accepted)]
	public async Task<IActionResult> CreateExportAsync([FromBody] HrReportRequestDto input)
	{
		var result = await reportAppService.CreateExportAsync(input);
		return Accepted(result);
	}

	[HttpGet("exports")]
	[ProducesResponseType(typeof(List<HrReportExportJobDto>), StatusCodes.Status200OK)]
	public Task<List<HrReportExportJobDto>> GetExportsAsync([FromQuery] HrReportExportListRequestDto input) =>
		reportAppService.GetExportsAsync(input);

	[HttpGet("exports/{jobId:guid}")]
	[ProducesResponseType(typeof(HrReportExportJobDto), StatusCodes.Status200OK)]
	public Task<HrReportExportJobDto> GetExportAsync(Guid jobId)
	{
		return reportAppService.GetExportAsync(jobId);
	}

	[HttpPost("exports/{jobId:guid}/cancel")]
	[ProducesResponseType(typeof(HrReportExportJobDto), StatusCodes.Status200OK)]
	public Task<HrReportExportJobDto> CancelExportAsync(Guid jobId)
	{
		return reportAppService.CancelExportAsync(jobId);
	}

	[HttpPost("exports/{jobId:guid}/retry")]
	[ProducesResponseType(typeof(HrReportExportCreateResponseDto), StatusCodes.Status202Accepted)]
	public Task<HrReportExportCreateResponseDto> RetryExportAsync(Guid jobId)
	{
		return reportAppService.RetryExportAsync(jobId);
	}

	[HttpPost("exports/{jobId:guid}/regenerate")]
	[ProducesResponseType(typeof(HrReportExportCreateResponseDto), StatusCodes.Status202Accepted)]
	public Task<HrReportExportCreateResponseDto> RegenerateExportAsync(Guid jobId)
	{
		return reportAppService.RegenerateExportAsync(jobId);
	}

	[HttpGet("exports/{jobId:guid}/download")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public async Task<IActionResult> DownloadExportAsync(Guid jobId)
	{
		var file = await reportAppService.DownloadExportAsync(jobId);
		return File(file.Bytes, file.ContentType, file.FileName);
	}

}