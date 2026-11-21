using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Custody;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Custody;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Authorize(Actions.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/custody")]
public class LibraryCustodyController(ILibraryCustodyAppService custody) : AbpController
{
	[HttpGet]
	public Task<PaginatedResponse<LibraryCustodyListItemDto>> GetListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
		custody.GetListAsync(page, pageSize);

	[HttpGet("{id:guid}")]
	public Task<LibraryCustodyDetailDto> GetAsync(Guid id) => custody.GetAsync(id);

	[HttpGet("options")]
	[Authorize(Actions.Add)]
	public Task<LibraryCustodyOptionsDto> GetOptionsAsync([FromQuery] Guid academicYearId) => custody.GetOptionsAsync(academicYearId);

	[HttpGet("copies")]
	[Authorize(Actions.Add)]
	public Task<List<LibraryCustodyCopyOptionDto>> FindCopiesAsync([FromQuery] string search) => custody.FindCopiesAsync(search);

	[HttpPost("preview")]
	[Authorize(Actions.Add)]
	public Task<LibraryCustodyPreviewDto> PreviewHandoffAsync([FromBody] LibraryCustodyHandoffInput input) => custody.PreviewHandoffAsync(input);

	[HttpPost]
	[Authorize(Actions.Add)]
	public Task<LibraryCustodyHandoffDto> HandoffAsync([FromBody] LibraryCustodyHandoffCommand command) => custody.HandoffAsync(command);

	[HttpPost("{id:guid}/returns/preview")]
	[Authorize(Actions.Return)]
	public Task<LibraryCustodyPreviewDto> PreviewReturnAsync(Guid id, [FromBody] LibraryCustodyReturnInput input) => custody.PreviewReturnAsync(id, input);

	[HttpPost("{id:guid}/returns")]
	[Authorize(Actions.Return)]
	public Task<LibraryCustodyReceiptDto> ReturnAsync(Guid id, [FromBody] LibraryCustodyReturnCommand command) => custody.ReturnAsync(id, command);
}
