using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Policies;

[Area("app"), RemoteService(Name = "Default"), Authorize]
[Route("api/hr/setup-review/{academicYearId:guid}/{section}")]
public class HrSetupReviewController(IHrSetupReviewAppService service) : AbpControllerBase
{
	[HttpGet]
	public Task<HrSetupReviewDto> GetAsync(Guid academicYearId, string section, [FromQuery] HrSetupReviewQuery input) =>
		service.GetAsync(academicYearId, section, input);

	[HttpPost]
	public Task<HrSetupReviewDto> RecordAsync(Guid academicYearId, string section, [FromBody] RecordHrSetupReviewDto input) =>
		service.RecordAsync(academicYearId, section, input);
}
