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
public class HrReportController(IHrReportAppService reportAppService) : AbpControllerBase
{
	[HttpGet("sources")]
	[ProducesResponseType(typeof(HrReportSourcesDto), StatusCodes.Status200OK)]
	public Task<HrReportSourcesDto> GetSourcesAsync()
	{
		return reportAppService.GetSourcesAsync();
	}

	[HttpGet("options")]
	[ProducesResponseType(typeof(HrReportOptionsDto), StatusCodes.Status200OK)]
	public Task<HrReportOptionsDto> GetOptionsAsync()
	{
		return reportAppService.GetOptionsAsync();
	}

	[HttpGet("templates")]
	[ProducesResponseType(typeof(HrReportTemplateListDto), StatusCodes.Status200OK)]
	public Task<HrReportTemplateListDto> GetTemplatesAsync()
	{
		return reportAppService.GetTemplatesAsync();
	}

	[HttpPost("templates")]
	[ProducesResponseType(typeof(HrReportTemplateDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateTemplateAsync([FromBody] HrReportTemplateRequestDto input)
	{
		var template = await reportAppService.CreateTemplateAsync(input);
		return Created($"api/hr/reports/templates/{template.Id}", template);
	}

	[HttpPut("templates/{id:guid}")]
	[ProducesResponseType(typeof(HrReportTemplateDto), StatusCodes.Status200OK)]
	public Task<HrReportTemplateDto> UpdateTemplateAsync(Guid id, [FromBody] HrReportTemplateRequestDto input)
	{
		return reportAppService.UpdateTemplateAsync(id, input);
	}

	[HttpPost("templates/{id:guid}/duplicate")]
	[ProducesResponseType(typeof(HrReportTemplateDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> DuplicateTemplateAsync(Guid id)
	{
		var template = await reportAppService.DuplicateTemplateAsync(id);
		return Created($"api/hr/reports/templates/{template.Id}", template);
	}

	[HttpDelete("templates/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteTemplateAsync(Guid id)
	{
		await reportAppService.DeleteTemplateAsync(id);
		return NoContent();
	}

	[HttpPost("preview")]
	[ProducesResponseType(typeof(HrReportPreviewDto), StatusCodes.Status200OK)]
	public Task<HrReportPreviewDto> PreviewAsync([FromBody] HrReportRequestDto input)
	{
		return reportAppService.PreviewAsync(input);
	}

	[HttpPost("exports")]
	[ProducesResponseType(typeof(HrReportExportCreateResponseDto), StatusCodes.Status202Accepted)]
	public async Task<IActionResult> CreateExportAsync([FromBody] HrReportRequestDto input)
	{
		var result = await reportAppService.CreateExportAsync(input);
		return Accepted(result);
	}

	[HttpGet("exports")]
	[ProducesResponseType(typeof(List<HrReportExportJobDto>), StatusCodes.Status200OK)]
	public Task<List<HrReportExportJobDto>> GetExportsAsync()
	{
		return reportAppService.GetExportsAsync();
	}

	[HttpGet("exports/{jobId:guid}")]
	[ProducesResponseType(typeof(HrReportExportJobDto), StatusCodes.Status200OK)]
	public Task<HrReportExportJobDto> GetExportAsync(Guid jobId)
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
