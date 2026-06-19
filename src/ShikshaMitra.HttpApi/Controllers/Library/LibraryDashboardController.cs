using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[RemoteService(Name = "Default")]
[Route("api/library/dashboard")]
public class LibraryDashboardController(ILibraryDashboardAppService dashboardAppService) : AbpControllerBase
{
	[HttpGet("overview")]
	public Task<LibraryDashboardOverviewDto> GetOverviewAsync([FromQuery] Guid? academicYearId = null)
	{
		return dashboardAppService.GetOverviewAsync(academicYearId);
	}
}
