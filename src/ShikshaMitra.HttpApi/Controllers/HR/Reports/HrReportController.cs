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

	[HttpGet("recipients")]
	[ProducesResponseType(typeof(List<HrReportRecipientDto>), StatusCodes.Status200OK)]
	public Task<List<HrReportRecipientDto>> GetRecipientsAsync(
		[FromQuery] string? filter = null,
		[FromQuery] int maxResultCount = 20,
		[FromQuery] string? ids = null)
	{
		return reportAppService.GetRecipientsAsync(filter, maxResultCount, ids);
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

	[HttpPost("preview-v2")]
	[ProducesResponseType(typeof(HrReportPreviewDto), StatusCodes.Status200OK)]
	public Task<HrReportPreviewDto> GetPreviewAsync([FromBody] HrReportRequestDto input)
	{
		return reportAppService.GetPreviewAsync(input);
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

	[HttpPost("exports/{jobId:guid}/share")]
	[ProducesResponseType(typeof(HrReportExportJobDto), StatusCodes.Status200OK)]
	public Task<HrReportExportJobDto> ShareExportAsync(Guid jobId, [FromBody] HrReportShareExportRequestDto input)
	{
		return reportAppService.ShareExportAsync(jobId, input);
	}

	[HttpGet("exports/{jobId:guid}/download")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	public async Task<IActionResult> DownloadExportAsync(Guid jobId)
	{
		var file = await reportAppService.DownloadExportAsync(jobId);
		return File(file.Bytes, file.ContentType, file.FileName);
	}

	[HttpGet("schedules")]
	[ProducesResponseType(typeof(HrReportScheduleListDto), StatusCodes.Status200OK)]
	public Task<HrReportScheduleListDto> GetSchedulesAsync()
	{
		return reportAppService.GetSchedulesAsync();
	}

	[HttpPost("schedules")]
	[ProducesResponseType(typeof(HrReportScheduleDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateScheduleAsync([FromBody] HrReportScheduleRequestDto input)
	{
		var schedule = await reportAppService.CreateScheduleAsync(input);
		return Created($"api/hr/reports/schedules/{schedule.Id}", schedule);
	}

	[HttpPut("schedules/{id:guid}")]
	[ProducesResponseType(typeof(HrReportScheduleDto), StatusCodes.Status200OK)]
	public Task<HrReportScheduleDto> UpdateScheduleAsync(Guid id, [FromBody] HrReportScheduleRequestDto input)
	{
		return reportAppService.UpdateScheduleAsync(id, input);
	}

	[HttpPost("schedules/{id:guid}/pause")]
	[ProducesResponseType(typeof(HrReportScheduleDto), StatusCodes.Status200OK)]
	public Task<HrReportScheduleDto> PauseScheduleAsync(Guid id)
	{
		return reportAppService.PauseScheduleAsync(id);
	}

	[HttpPost("schedules/{id:guid}/resume")]
	[ProducesResponseType(typeof(HrReportScheduleDto), StatusCodes.Status200OK)]
	public Task<HrReportScheduleDto> ResumeScheduleAsync(Guid id)
	{
		return reportAppService.ResumeScheduleAsync(id);
	}

	[HttpPost("schedules/{id:guid}/run-now")]
	[ProducesResponseType(typeof(HrReportScheduleRunResponseDto), StatusCodes.Status202Accepted)]
	public Task<HrReportScheduleRunResponseDto> RunScheduleNowAsync(Guid id)
	{
		return reportAppService.RunScheduleNowAsync(id);
	}

	[HttpDelete("schedules/{id:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteScheduleAsync(Guid id)
	{
		await reportAppService.DeleteScheduleAsync(id);
		return NoContent();
	}
}
