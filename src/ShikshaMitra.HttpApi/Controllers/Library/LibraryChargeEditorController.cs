using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Charges.Editor;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/charges")]
public class LibraryChargeEditorController(ILibraryChargeEditorLookupAppService editor) : AbpController
{
	[HttpGet("editor/categories")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public Task<PaginatedResponse<LibraryChargeEditorCategoryDto>> GetCategoriesAsync([FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
		editor.GetCategoriesAsync(academicYearId, page, pageSize, search);

	[HttpGet("editor/members")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public Task<PaginatedResponse<LibraryChargeEditorMemberDto>> SearchMembersAsync([FromQuery] Guid academicYearId,
		[FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		editor.SearchMembersAsync(academicYearId, search, page, pageSize);

	[HttpGet("editor/member")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public Task<LibraryChargeEditorMemberDto> ResolveMemberAsync([FromQuery] Guid academicYearId, [FromQuery] string identifier) =>
		editor.ResolveMemberAsync(academicYearId, identifier);

	[HttpGet("editor/loans")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Add)]
	public Task<PaginatedResponse<LibraryChargeEditorLoanSourceDto>> GetLoanSourcesAsync([FromQuery] Guid academicYearId,
		[FromQuery] Guid memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
		editor.GetLoanSourcesAsync(academicYearId, memberId, page, pageSize, search);

	[HttpGet("{chargeId:guid}/editor")]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Charges.Update)]
	public Task<LibraryChargeEditorContextDto> GetUpdateContextAsync(Guid chargeId) => editor.GetUpdateContextAsync(chargeId);
}
