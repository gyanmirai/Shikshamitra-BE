using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.PersonalInfo;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees/assignable-roles")]
[Authorize]
public class EmployeeAssignableRolesController(IEmployeeAssignableRoleAppService appService) : AbpControllerBase
{
	[HttpGet]
	public Task<ListResultDto<EmployeeAssignableRoleDto>> GetListAsync() => appService.GetListAsync();
}
