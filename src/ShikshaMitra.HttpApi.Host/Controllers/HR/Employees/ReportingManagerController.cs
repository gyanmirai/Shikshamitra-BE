using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.ReportingManager;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Route("api")]
[Authorize]
public class ReportingManagerController(IReportingManagerAppService reportingManagerAppService) 
	: AbpController
{
	/// <summary>
	/// Update or assign reporting manager of an employee
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <param name="input">The reporting manager assignment DTO</param>
	/// <returns>The employee reporting response with updated reporting manager</returns>
	[HttpPut("employees/{employeeId}/reporting-manager")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ReportingManager.Update)]
	public async Task<EmployeeReportingResponseDto> UpdateReportingManagerAsync(
		Guid employeeId, 
		[FromBody] AssignReportingManagerDto input)
	{
		return await reportingManagerAppService.AssignReportingManagerAsync(employeeId, input);
	}

	/// <summary>
	/// Get reporting manager of an employee
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <returns>The employee reporting response with reporting manager details</returns>
	[HttpGet("employees/{employeeId}/reporting-manager")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ReportingManager.View)]
	public async Task<EmployeeReportingResponseDto> GetReportingManagerAsync(Guid employeeId)
	{
		return await reportingManagerAppService.GetReportingManagerAsync(employeeId);
	}

	/// <summary>
	/// Get all employees under a reporting manager
	/// </summary>
	/// <param name="managerId">The ID of the manager</param>
	/// <returns>The response containing manager details and list of employees</returns>
	[HttpGet("reporting-managers/{managerId}/employees")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_ReportingManager.View)]
	public async Task<EmployeesByManagerResponseDto> GetEmployeesByManagerAsync(Guid managerId)
	{
		return await reportingManagerAppService.GetEmployeesByManagerAsync(managerId);
	}
}
