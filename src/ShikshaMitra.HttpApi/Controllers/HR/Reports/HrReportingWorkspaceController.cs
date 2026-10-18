using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Reports;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/reports")]
[Authorize]
public class HrReportingWorkspaceController(IHrReportingWorkspaceAppService workspace, IHrReportingTemplateService templates) : AbpControllerBase
{
	[HttpGet("catalogue")] public Task<List<HrReportDatasetDto>> CatalogueAsync() => workspace.GetCatalogueAsync();
	[HttpGet("workspace-options")] public Task<HrReportWorkspaceOptionsDto> OptionsAsync(string dataset, string search = "") => workspace.GetOptionsAsync(dataset, search);
	[HttpGet("library")] public Task<List<HrReportLibraryTemplateDto>> LibraryAsync(bool edit = false) => templates.GetLibraryAsync(edit);
	[HttpPost("library")] public Task<HrReportLibraryTemplateDto> CreateTemplateAsync(HrReportSaveDesignDto input) => templates.SaveAsync(null, input);
	[HttpPut("library/{id:guid}")] public Task<HrReportLibraryTemplateDto> UpdateTemplateAsync(Guid id, HrReportSaveDesignDto input) => templates.SaveAsync(id, input);
	[HttpPost("library/{id:guid}/archive")] public Task ArchiveAsync(Guid id, [FromQuery] string concurrencyStamp) => templates.ArchiveAsync(id, concurrencyStamp);
	[HttpPost("designer-preview")] public Task<HrReportingPreviewDto> PreviewAsync(HrReportDesignerPreviewDto input) => workspace.PreviewAsync(input);
	[HttpPost("runs")] public Task<HrReportRunDto> CreateAsync(HrReportCreateRunDto input) => workspace.CreateAsync(input);
	[HttpGet("runs")] public Task<HrReportRunHistoryDto> ListAsync(int skip = 0, int take = 50) => workspace.GetRunsAsync(skip, take);
	[HttpGet("runs/{id:guid}")] public Task<HrReportRunDto> GetAsync(Guid id) => workspace.GetAsync(id);
	[HttpGet("runs/{id:guid}/rows")] public Task<HrReportResultPageDto> RowsAsync(Guid id, int page = 1, int pageSize = 50) => workspace.GetRowsAsync(id, page, pageSize);
	[HttpPost("runs/{id:guid}/cancel")] public Task CancelAsync(Guid id) => workspace.CancelAsync(id);
	[HttpPost("runs/{id:guid}/retry")] public Task<HrReportRunDto> RetryAsync(Guid id) => workspace.RetryAsync(id);
	[HttpPost("runs/{id:guid}/exports")] public Task ExportAsync(Guid id, string format) => workspace.CreateExportAsync(id, format);
	[HttpGet("runs/{id:guid}/download")]
	public async Task<IActionResult> DownloadAsync(Guid id, string format)
	{
		var file = await workspace.DownloadAsync(id, format); return File(file.Bytes, file.ContentType, file.FileName);
	}
}
