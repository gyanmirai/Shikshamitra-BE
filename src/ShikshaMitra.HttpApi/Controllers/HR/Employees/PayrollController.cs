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
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class PayrollController : AbpControllerBase
{
	private readonly IEmployeePayrollAppService _payrollAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public PayrollController(
		IEmployeePayrollAppService payrollAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_payrollAppService = payrollAppService;
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
		await _authorizationService.CheckEmployeeAccessAsync(employeeId, requireOwnership: true);
		return await _payrollAppService.GetPayrollContractAsync(employeeId);
	}

	/// <summary>
	/// Backward-compatible alias for payroll fetch endpoint.
	/// </summary>
	[HttpGet("{employeeId}/get-payroll")]
	[ProducesResponseType(typeof(PayrollContract), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PayrollContract> GetLegacyAsync([FromRoute, Required] Guid employeeId)
	{
		return await GetAsync(employeeId);
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
		await _authorizationService.CheckSensitiveFieldAccessAsync(employeeId);
		await _payrollAppService.UpdatePayrollContractAsync(employeeId, request);
		return Ok();
	}
}
