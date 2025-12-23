using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Controller for managing employee status.
/// </summary>
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/employees/{employeeId}/status")]
[Authorize]
public class EmployeeStatusController : AbpController
{
	private readonly IEmployeeStatusAppService _employeeStatusAppService;

	public EmployeeStatusController(IEmployeeStatusAppService employeeStatusAppService)
	{
		_employeeStatusAppService = employeeStatusAppService;
	}

	/// <summary>
	/// PUT /api/employees/{employeeId}/status
	/// Changes the status of an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <param name="input">The status change request</param>
	/// <returns>Response containing previous and current status</returns>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeeStatusResponseDto> ChangeStatusAsync(
		Guid employeeId,
		[FromBody] ChangeEmployeeStatusDto input)
	{
		return await _employeeStatusAppService.ChangeStatusAsync(employeeId, input);
	}
}

