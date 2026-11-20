using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Policies;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Policies;

[Authorize]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/policies/rollover")]
public class LibraryRolloverController(ILibraryRolloverAppService rollover) : AbpController
{
	[HttpGet("options")]
	public Task<List<LibraryRolloverYearDto>> GetOptionsAsync() => rollover.GetOptionsAsync();

	[HttpPost("preview")]
	public Task<LibraryRolloverPreviewDto> PreviewAsync([FromBody] LibraryRolloverInput input) => rollover.PreviewAsync(input);
}
