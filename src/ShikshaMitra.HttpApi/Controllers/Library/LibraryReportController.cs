using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Reports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[RemoteService(Name = "Default")]
[Route("api/library/reports")]
public class LibraryReportController(ILibraryReportAppService reportAppService) : AbpControllerBase
{
	[HttpGet("options")]
	[ProducesResponseType(typeof(LibraryReportOptionsDto), StatusCodes.Status200OK)]
	public Task<LibraryReportOptionsDto> GetOptionsAsync()
	{
		return reportAppService.GetOptionsAsync();
	}

	[HttpPost("preview")]
	[ProducesResponseType(typeof(LibraryReportPreviewDto), StatusCodes.Status200OK)]
	public Task<LibraryReportPreviewDto> PreviewAsync([FromBody] LibraryReportRequestDto input)
	{
		return reportAppService.PreviewAsync(input);
	}

	[HttpPost("exports")]
	[ProducesResponseType(typeof(LibraryReportExportCreateResponseDto), StatusCodes.Status202Accepted)]
	public async Task<IActionResult> CreateExportAsync([FromBody] LibraryReportRequestDto input)
	{
		var result = await reportAppService.CreateExportAsync(input);
		return Accepted(result);
	}

	[HttpGet("exports")]
	[ProducesResponseType(typeof(List<LibraryReportExportJobDto>), StatusCodes.Status200OK)]
	public Task<List<LibraryReportExportJobDto>> GetExportsAsync()
	{
		return reportAppService.GetExportsAsync();
	}

	[HttpGet("exports/{jobId:guid}")]
	[ProducesResponseType(typeof(LibraryReportExportJobDto), StatusCodes.Status200OK)]
	public Task<LibraryReportExportJobDto> GetExportAsync(Guid jobId)
	{
		return reportAppService.GetExportAsync(jobId);
	}

	[HttpGet("exports/{jobId:guid}/download")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public async Task<IActionResult> DownloadExportAsync(Guid jobId)
	{
		var file = await reportAppService.DownloadExportAsync(jobId);
		return File(file.Bytes, file.ContentType, file.FileName);
	}
}
