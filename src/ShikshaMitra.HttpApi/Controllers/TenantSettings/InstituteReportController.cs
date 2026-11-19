using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.TenantSettings.Reports;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.TenantSettings;

[Authorize(InstituteReportPermissions.View)]
[Route("api/app/institute-setup-report")]
public class InstituteReportController(IInstituteReportAppService service) : AbpControllerBase
{
	[HttpGet("options")] public Task<InstituteReportOptions> Options() => service.GetOptionsAsync();
	[HttpPost("preview")] public Task<AcademicReportPreviewDto> Preview([FromBody] InstituteReportRequest input) => service.PreviewAsync(input);
	[HttpPost("exports"), Authorize(InstituteReportPermissions.Add)] public Task<InstituteReportJobDto> Export([FromBody] InstituteReportRequest input) => service.CreateExportAsync(input);
	[HttpGet("exports")] public Task<List<InstituteReportJobDto>> Exports() => service.GetExportsAsync();
	[HttpGet("history")] public Task<PagedResultDto<InstituteReportHistoryRowDto>> History([FromQuery] InstituteReportHistoryInput input) => service.GetHistoryAsync(input);
	[HttpPost("statuses")] public Task<List<InstituteReportHistoryRowDto>> Statuses([FromBody] List<Guid> ids) => service.GetStatusesAsync(ids);
	[HttpGet("exports/{jobId:guid}")] public Task<InstituteReportJobDto> Job(Guid jobId) => service.GetExportAsync(jobId);
	[HttpGet("exports/{jobId:guid}/download")] public async Task<IActionResult> Download(Guid jobId)
	{
		var file = await service.DownloadAsync(jobId);
		Response.Headers.CacheControl = "private, no-store";
		Response.Headers.XContentTypeOptions = "nosniff";
		return File(file.Bytes, file.ContentType, file.FileName);
	}
	[HttpPost("ai/plan"), Authorize(InstituteReportPermissions.Add)] public Task<InstituteAiPlanResult> Plan([FromBody] InstituteAiPlanInput input) => service.PlanAsync(input);
	[HttpPost("ai/preview"), Authorize(InstituteReportPermissions.Add)] public Task<AcademicReportPreviewDto> AiPreview([FromBody] InstituteAiApprovedRequest input) => service.AiPreviewAsync(input);
	[HttpPost("ai/exports"), Authorize(InstituteReportPermissions.Add)] public Task<InstituteReportJobDto> AiExport([FromBody] InstituteAiApprovedRequest input) => service.AiExportAsync(input);
}
