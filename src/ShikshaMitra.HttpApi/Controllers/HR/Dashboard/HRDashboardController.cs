using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Dashboard;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/dashboard")]
[Authorize]
public class HRDashboardController(IHRDashboardAppService dashboardAppService) : AbpControllerBase
{
	[HttpGet("overview")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
	public virtual Task<HRDashboardOverviewDto> GetOverviewAsync([FromQuery] HRDashboardOverviewInput input) =>
		dashboardAppService.GetOverviewAsync(input);

	[HttpGet("staff")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View)]
	public virtual Task<HRDashboardStaffPageDto> GetStaffAsync([FromQuery] HRDashboardStaffInput input) =>
		dashboardAppService.GetStaffAsync(input);

	[HttpGet("previews/{dashboardKey}")]
	[ProducesResponseType(typeof(HRDashboardPreviewDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<HRDashboardPreviewDto> GetPreviewAsync([FromRoute] string dashboardKey)
	{
		return await dashboardAppService.GetPreviewAsync(dashboardKey);
	}

	[HttpPost("previews/{dashboardKey}/refresh")]
	[ProducesResponseType(typeof(HRDashboardPreviewDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<HRDashboardPreviewDto> RefreshPreviewAsync([FromRoute] string dashboardKey)
	{
		return await dashboardAppService.RefreshPreviewAsync(dashboardKey);
	}
}
