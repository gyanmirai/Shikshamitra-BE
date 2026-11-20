using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.CampusTransfers;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.CampusTransfers;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize(Actions.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/campus-transfers")]
public class LibraryCampusTransferController(ILibraryCampusTransferAppService transfers) : AbpController
{
	[HttpGet]
	public Task<PaginatedResponse<LibraryCampusTransferListItem>> GetListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
		transfers.GetListAsync(page, pageSize);

	[HttpGet("{id:guid}")]
	public Task<LibraryCampusTransferDetail> GetAsync(Guid id) => transfers.GetAsync(id);

	[HttpGet("locations")]
	[Authorize(Actions.Dispatch)]
	public Task<List<LibraryCampusTransferSection>> GetLocationsAsync() => transfers.GetLocationsAsync();

	[HttpGet("copies")]
	[Authorize(Actions.Dispatch)]
	public Task<List<LibraryCampusTransferCopyOption>> FindCopiesAsync([FromQuery] Guid sourceSectionId, [FromQuery] string search) =>
		transfers.FindCopiesAsync(sourceSectionId, search);

	[HttpPost("preview")]
	[Authorize(Actions.Dispatch)]
	public Task<LibraryCampusTransferPreview> PreviewDispatchAsync([FromBody] LibraryCampusDispatchInput input) => transfers.PreviewDispatchAsync(input);

	[HttpPost]
	[Authorize(Actions.Dispatch)]
	public Task<LibraryCampusTransferHeader> DispatchAsync([FromBody] LibraryCampusDispatchCommand command) => transfers.DispatchAsync(command);

	[HttpPost("{id:guid}/receipts/preview")]
	[Authorize(Actions.Receive)]
	public Task<LibraryCampusTransferPreview> PreviewReceiveAsync(Guid id, [FromBody] LibraryCampusReceiveInput input) => transfers.PreviewReceiveAsync(id, input);

	[HttpPost("{id:guid}/receipts")]
	[Authorize(Actions.Receive)]
	public Task<LibraryCampusTransferReceiptDto> ReceiveAsync(Guid id, [FromBody] LibraryCampusReceiveCommand command) => transfers.ReceiveAsync(id, command);
}
