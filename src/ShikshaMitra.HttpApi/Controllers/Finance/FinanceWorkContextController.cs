using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance;

[RemoteService]
[Area("finance")]
[ApiController]
[Route("api/finance/work-context")]
[Authorize]
public class FinanceWorkContextController(IFinanceWorkContextAppService workContext) : AbpController
{
	[HttpGet]
	public Task<FinanceWorkContextDto> GetAsync([FromQuery] Guid academicYearId) =>
		workContext.GetAsync(academicYearId);
}
