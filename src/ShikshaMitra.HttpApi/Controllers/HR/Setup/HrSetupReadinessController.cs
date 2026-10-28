using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Setup;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Setup;

[Area("app"), RemoteService(Name = "Default"), Authorize]
[Route("api/hr/setup-readiness")]
public class HrSetupReadinessController(IHrSetupReadinessAppService service) : AbpControllerBase
{
	[HttpGet]
	[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
	public Task<HrSetupReadinessDto> GetAsync([FromQuery] Guid? academicYearId = null) => service.GetAsync(academicYearId);
}
