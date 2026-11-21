using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Catalogue;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.View)]
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/library/catalogue/configuration")]
public class LibraryCatalogueConfigurationController(ILibraryCatalogueConfigurationAppService configuration) : AbpController
{
	[HttpGet]
	public Task<LibraryCatalogueConfigurationDto> GetAsync() => configuration.GetAsync();

	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_CatalogueConfiguration.Update)]
	public Task<LibraryCatalogueConfigurationDto> UpdateAsync([FromBody] LibraryCatalogueConfigurationRequest input) => configuration.UpdateAsync(input);
}
