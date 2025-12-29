using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalaryStructure;

/// <summary>
/// Controller for managing employee salary structure.
/// </summary>
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/payroll/employees/{employeeId}/salary-structure")]
[Authorize]
public class EmployeeSalaryStructureController : AbpController
{
	private readonly IEmployeeSalaryStructureAppService _salaryStructureAppService;

	public EmployeeSalaryStructureController(IEmployeeSalaryStructureAppService salaryStructureAppService)
	{
		_salaryStructureAppService = salaryStructureAppService;
	}

	/// <summary>
	/// GET /api/payroll/employees/{employeeId}/salary-structure
	/// Gets the salary structure for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <returns>The employee's salary structure</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<SalaryStructureResponseDto> GetAsync(Guid employeeId)
	{
		return await _salaryStructureAppService.GetAsync(employeeId);
	}

	/// <summary>
	/// PUT /api/payroll/employees/{employeeId}/salary-structure
	/// Updates the salary structure for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <param name="input">The salary structure to set</param>
	/// <returns>Success response</returns>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<IActionResult> UpdateAsync(
		Guid employeeId,
		[FromBody] UpdateSalaryStructureDto input)
	{
		await _salaryStructureAppService.UpdateAsync(employeeId, input);
		
		return Ok(new
		{
			message = "Salary structure updated successfully"
		});
	}
}

