using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.AcademicSetups;

[Authorize]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/app/academic-field-formatting")]
public class AcademicFieldFormattingController(IAcademicFieldFormattingAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View)]
	public Task<AcademicFieldFormattingDto> GetAsync() => service.GetAsync();

	[HttpPost("preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View)]
	public Task<AcademicNamePreviewDto> PreviewAsync([FromBody] AcademicNamePreviewInput input) => service.PreviewAsync(input);

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit)]
	public Task<AcademicFieldFormattingDto> UpdateAsync([FromBody] AcademicFieldFormattingDto input) => service.UpdateAsync(input);
}
