using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;
using ShikshaMitra.TenantSettings.Formatting;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings;

[Authorize]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/app/institute-field-formatting/{scope}")]
public class InstituteFieldFormattingController(IInstituteFieldFormattingAppService service) : AbpController
{
	[HttpGet]
	public Task<InstituteFieldFormattingDto> GetAsync(string scope) => service.GetAsync(scope);

	[HttpPut]
	public Task<InstituteFieldFormattingDto> UpdateAsync(string scope, [FromBody] InstituteFieldFormattingDto input) => service.UpdateAsync(scope, input);

	[HttpPost("preview")]
	public Task<AcademicNamePreviewDto> PreviewAsync(string scope, [FromBody] InstituteNamePreviewInput input) => service.PreviewAsync(scope, input);
}
