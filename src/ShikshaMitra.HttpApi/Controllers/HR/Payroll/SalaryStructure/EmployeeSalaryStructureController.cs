using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Controllers.HR.Payroll.SalaryStructure;

/// <summary>
/// Controller for managing employee salary structure.
/// </summary>
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/payroll/employees/{employeeId}/salary-structure")]
[Authorize]
public class EmployeeSalaryStructureController : AbpControllerBase
{
	private readonly IEmployeeSalaryStructureAppService _salaryStructureAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public EmployeeSalaryStructureController(
		IEmployeeSalaryStructureAppService salaryStructureAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_salaryStructureAppService = salaryStructureAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// GET /api/payroll/employees/{employeeId}/salary-structure
	/// Gets the salary structure for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <returns>The employee's salary structure</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<ActionResult<SalaryStructureResponseDto>> GetAsync(Guid employeeId)
	{
		try
		{
			await _authorizationService.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);
			var response = await _salaryStructureAppService.GetAsync(employeeId);
			return Ok(response);
		}
		catch (EntityNotFoundException)
		{
			return NotFound();
		}
	}

	/// <summary>
	/// PUT /api/payroll/employees/{employeeId}/salary-structure
	/// Updates the salary structure for an employee.
	/// </summary>
	/// <param name="employeeId">The ID of the employee</param>
	/// <param name="input">The salary structure to set</param>
	/// <returns>Success response</returns>
	[HttpPut]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_PayrollStructure.Add)]
	public async Task<IActionResult> UpdateAsync(
		Guid employeeId,
		[FromBody] UpdateSalaryStructureDto input)
	{
		try
		{
			await _authorizationService.CheckSensitiveFieldAccessAsync(employeeId);
			await _salaryStructureAppService.UpdateAsync(employeeId, input);
			return Ok();
		}
		catch (EntityNotFoundException)
		{
			return NotFound();
		}
	}
}

