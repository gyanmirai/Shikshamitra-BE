using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Dashboard;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Dashboard;

[Area("app"), RemoteService(Name = "Default"), Route("api/academic/dashboard/insights"), Authorize]
public class AcademicInsightsController(IAcademicInsightsAppService insights) : AbpControllerBase
{
	[HttpGet]
	public Task<AcademicInsightsDto> GetAsync([FromQuery] AcademicInsightsInput input) => insights.GetAsync(input);
}
