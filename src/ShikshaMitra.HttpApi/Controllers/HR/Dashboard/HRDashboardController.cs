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
	[HttpGet("summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
	[ProducesResponseType(typeof(HRDashboardSummaryDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<HRDashboardSummaryDto> GetSummaryAsync(
		[FromQuery] string? date = null,
		[FromQuery] string? month = null,
		[FromQuery] int? range = null)
	{
		return await dashboardAppService.GetSummaryAsync(new HRDashboardSummaryRequestDto
		{
			Date = date,
			Month = month,
			Range = range
		});
	}

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
