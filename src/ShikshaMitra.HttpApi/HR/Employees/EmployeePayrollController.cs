using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Payroll;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Employees;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/employees/{employeeId}/payroll")]
[Authorize]
public class EmployeePayrollController : AbpController
{
	private readonly IEmployeeAppService _employeeAppService;

	public EmployeePayrollController(IEmployeeAppService employeeAppService)
	{
		_employeeAppService = employeeAppService;
	}

	/// <summary>
	/// GET /api/employees/{employeeId}/payroll
	/// Gets the complete payroll details for an employee including earnings and deductions.
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<EmployeePayrollDetailsDto> GetPayrollDetailsAsync(Guid employeeId)
	{
		return await _employeeAppService.GetPayrollDetailsAsync(employeeId);
	}

	/// <summary>
	/// PUT /api/employees/{employeeId}/payroll
	/// Updates the complete payroll details for an employee.
	/// </summary>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<IActionResult> UpdatePayrollDetailsAsync(
		Guid employeeId,
		[FromBody] UpdateEmployeePayrollDetailsDto input)
	{
		await _employeeAppService.UpdatePayrollDetailsAsync(employeeId, input);
		
		return Ok(new
		{
			message = "Payroll details updated successfully",
			employeeId = employeeId.ToString()
		});
	}
}
