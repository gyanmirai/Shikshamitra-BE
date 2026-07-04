using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Dashboard;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IMainDashboardAppService dashboardAppService) : AbpControllerBase
{
	[HttpGet("summary")]
	[Authorize(ShikshaMitraModulePermissions.Dashboard)]
	[ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual Task<DashboardSummaryDto> GetSummaryAsync(
		[FromQuery] string? dashboardTemplate = null,
		[FromQuery] string? academicYearId = null,
		[FromQuery] string? from = null,
		[FromQuery] string? to = null) =>
		dashboardAppService.GetSummaryAsync(new DashboardSummaryRequestDto
		{
			DashboardTemplate = dashboardTemplate,
			AcademicYearId = academicYearId,
			From = from,
			To = to
		});

	[HttpGet("widgets")]
	[Authorize(ShikshaMitraModulePermissions.Dashboard)]
	[ProducesResponseType(typeof(List<DashboardWidgetMetadataDto>), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual Task<List<DashboardWidgetMetadataDto>> GetWidgetMetadataAsync() =>
		dashboardAppService.GetWidgetMetadataAsync();
}
