using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement.Dashboard;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[Authorize]
[ApiController]
public class ClassManagementDashboardController(
	IClassManagementDashboardAppService dashboardAppService
) : AbpController
{
	[HttpGet]
	[Route("api/class-management/dashboard")]
	[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.Dashboard.View)]
	[ProducesResponseType(typeof(ClassManagementDashboardDto), 200)]
	public async Task<IActionResult> GetDashboardAsync(
		[FromQuery] Guid academicMediumId,
		[FromQuery] string? role = null)
	{
		var result = await dashboardAppService.GetDashboardAsync(academicMediumId, role);
		return Ok(result);
	}
}
