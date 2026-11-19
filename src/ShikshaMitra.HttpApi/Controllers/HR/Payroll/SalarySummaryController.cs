using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/employees/{employeeId:guid}/salary-summary")]
[Authorize]
public class SalarySummaryController(ISalarySummaryAppService service) : AbpControllerBase
{
	[HttpGet]
	public Task<SalarySummaryDto> GetAsync(Guid employeeId, [FromQuery] string month) => service.GetAsync(employeeId, month);
}
