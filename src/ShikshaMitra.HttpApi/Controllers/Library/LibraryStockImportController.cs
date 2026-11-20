using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Receiving;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using ReceivingActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Receiving;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Authorize(ReceivingActions.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/receiving/imports")]
public class LibraryStockImportController(ILibraryStockImportAppService imports) : AbpController
{
	[HttpGet("options")]
	[Authorize(ReceivingActions.Add)]
	public Task<LibraryStockImportOptionsDto> GetOptionsAsync([FromQuery] Guid academicYearId) => imports.GetOptionsAsync(academicYearId);

	[HttpPost("preview")]
	[RequestSizeLimit(7 * 1024 * 1024)]
	[Authorize(ReceivingActions.Add)]
	public Task<LibraryStockImportPreviewDto> PreviewAsync([FromBody] LibraryStockImportInput input) => imports.PreviewAsync(input);

	[HttpPost]
	[RequestSizeLimit(7 * 1024 * 1024)]
	[Authorize(ReceivingActions.Add)]
	public Task<LibraryStockImportBatchDto> CommitAsync([FromBody] LibraryCommitStockImportCommand command) => imports.CommitAsync(command);

	[HttpGet]
	public Task<PaginatedResponse<LibraryStockImportSummaryDto>> GetListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
		imports.GetListAsync(page, pageSize);

	[HttpGet("{id:guid}")]
	public Task<LibraryStockImportBatchDto> GetAsync(Guid id) => imports.GetAsync(id);
}
