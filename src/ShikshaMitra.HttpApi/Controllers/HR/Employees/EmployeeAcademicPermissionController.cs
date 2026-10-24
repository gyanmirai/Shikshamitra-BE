using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.AcademicPermissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees/{employeeId:guid}/academic-permission")]
[Authorize]
public class EmployeeAcademicPermissionController(IEmployeeAcademicPermissionAppService service) : AbpControllerBase
{
	[HttpGet]
	public Task<UserScopeHierarchyDto> GetAsync(Guid employeeId, [FromQuery] Guid academicYearId) =>
		service.GetAsync(employeeId, academicYearId);

	[HttpPut]
	public Task<BulkScopeAssignmentResultDto> UpdateAsync(Guid employeeId, [FromBody] BulkScopeAssignmentDto input) =>
		service.UpdateAsync(employeeId, input);
}
