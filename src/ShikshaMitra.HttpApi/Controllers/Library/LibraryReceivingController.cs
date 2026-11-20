using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Receiving;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/receiving")]
public class LibraryReceivingController(ILibraryReceivingAppService receiving) : AbpController
{
	[HttpGet]
	public Task<ShikshaMitra.Library.Shared.PaginatedResponse<LibraryStockReceiptSummaryDto>> GetListAsync([FromQuery] LibraryReceivingListInput input)
		=> receiving.GetListAsync(input);

	[HttpGet("options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public Task<LibraryReceivingOptionsDto> GetOptionsAsync() => receiving.GetOptionsAsync();

	[HttpPost("preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public Task<LibraryReceivingPreviewDto> PreviewAsync([FromBody] LibraryReceivingInput input) => receiving.PreviewAsync(input);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Receiving.Add)]
	public Task<LibraryStockReceiptDto> ReceiveAsync([FromBody] LibraryReceiveCommand input) => receiving.ReceiveAsync(input);

	[HttpGet("{id:guid}")]
	public Task<LibraryStockReceiptDto> GetAsync(Guid id) => receiving.GetAsync(id);
}
