using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.Statutory;

[Area("app"), RemoteService(Name = "Default"), Authorize, Route("api/employees/{employeeId:guid}/payroll/statutory-profile")]
public class EmployeeStatutoryProfileController(IEmployeeStatutoryProfileAppService service) : AbpControllerBase
{
	[HttpGet("/api/payroll/statutory-profile/readiness")]
	public Task<PayrollRegistrationReadinessDto> GetReadinessAsync([FromQuery] string? month = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null) => service.GetReadinessAsync(month, page, pageSize, search);
	[HttpGet] public Task<EmployeeStatutoryProfileDto> GetAsync(Guid employeeId, [FromQuery] string? month = null) => service.GetAsync(employeeId, month);
	[HttpPut] public Task<EmployeeStatutoryProfileDto> SaveAsync(Guid employeeId, SaveEmployeeStatutoryProfileDto input) => service.SaveAsync(employeeId, input);
}
