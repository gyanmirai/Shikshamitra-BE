using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app"), RemoteService(Name = "Default"), Route("api/employees/{employeeId:guid}/account-email")]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit), Authorize(IdentityPermissions.Users.Update)]
public class EmployeeAccountEmailController(IEmployeeAccountEmailAppService service) : AbpControllerBase
{
	[HttpGet]
	public Task<EmployeeAccountEmailDto> GetAsync(Guid employeeId) => service.GetAsync(employeeId);
	[HttpPut]
	public Task<EmployeeAccountEmailDto> ChangeAsync(Guid employeeId, [FromBody] ChangeEmployeeAccountEmailDto input) => service.ChangeAsync(employeeId, input);
}