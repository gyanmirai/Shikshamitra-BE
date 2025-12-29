using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.Payroll;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee payroll information management controller
/// Handles operations for managing employee payroll data in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class PayrollController : AbpControllerBase
{
	private readonly IEmployeeAppService _employeeAppService;

	public PayrollController(IEmployeeAppService employeeAppService)
	{
		_employeeAppService = employeeAppService;
	}

	/// <summary>
	/// Get employee payroll information (matches OpenAPI Payroll contract)
	/// </summary>
	[HttpGet("{employeeId}/payroll")]
	[ProducesResponseType(typeof(PayrollContract), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PayrollContract> GetAsync([FromRoute, Required] Guid employeeId)
	{
		return await _employeeAppService.GetPayrollContractAsync(employeeId);
	}

	/// <summary>
	/// Update employee payroll information (matches OpenAPI Payroll contract)
	/// </summary>
	[HttpPut("{employeeId}/payroll")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] PayrollContract request)
	{
		await _employeeAppService.UpdatePayrollContractAsync(employeeId, request);
		return Ok();
	}
}
