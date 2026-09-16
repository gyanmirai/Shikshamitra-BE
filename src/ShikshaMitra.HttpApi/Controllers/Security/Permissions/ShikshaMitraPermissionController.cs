using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Security.Permissions;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Security.Permissions;

[Microsoft.AspNetCore.Authorization.Authorize]
[Route("api/shikshamitra-permissions")]
public class ShikshaMitraPermissionController(IShikshaMitraPermissionAppService permissionService) : AbpController
{
	[HttpGet("permissions")]
	public async Task<IActionResult> GetNestedPermissions([FromQuery] string providerName, [FromQuery] string providerKey)
	{
		var result = await permissionService.GetNestedAsync(providerName, providerKey);
		return Ok(result);
	}
}
