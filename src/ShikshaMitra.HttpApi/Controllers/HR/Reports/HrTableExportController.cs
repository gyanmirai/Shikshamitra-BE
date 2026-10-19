using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Reports;

[Authorize, Area("app"), RemoteService(Name = "Default"), Route("api/hr/table-exports")]
public class HrTableExportController(IHrTableExportAppService exports) : AbpControllerBase
{
	[HttpPost] public Task<HrTableExportStatus> CreateAsync(HrTableExportRequest request) => exports.CreateAsync(request);
	[HttpGet("{id:guid}")] public Task<HrTableExportStatus> GetAsync(Guid id) => exports.GetAsync(id);
	[HttpPost("{id:guid}/cancel")] public Task CancelAsync(Guid id) => exports.CancelAsync(id);
	[HttpGet("{id:guid}/download")]
	public async Task<IActionResult> DownloadAsync(Guid id)
	{
		var file = await exports.DownloadAsync(id);
		return File(file.Bytes, file.ContentType, file.FileName);
	}
}
