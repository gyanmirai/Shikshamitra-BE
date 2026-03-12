using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
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
	private readonly IEmployeeAuthorizationService _authorizationService;

	public PayrollController(
		IEmployeeAppService employeeAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_employeeAppService = employeeAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Get employee payroll information (matches OpenAPI Payroll contract)
	/// </summary>
	[HttpGet("{employeeId}/payroll")]
	[ProducesResponseType(typeof(PayrollContract), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PayrollContract> GetAsync([FromRoute, Required] Guid employeeId)
	{
		// Check access rights - sensitive data (IDOR protection + ownership check)
		await _authorizationService.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);

		return await _employeeAppService.GetPayrollContractAsync(employeeId);
	}

	/// <summary>
	/// Update employee payroll information (matches OpenAPI Payroll contract)
	/// </summary>
	[HttpPut("{employeeId}/payroll")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] PayrollContract request)
	{
		// Check sensitive field access (only HR Admin)
		await _authorizationService.CheckSensitiveFieldAccessAsync(employeeId);

		await _employeeAppService.UpdatePayrollContractAsync(employeeId, request);
		return Ok();
	}
}
