using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.PayrollInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Employees;

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
	/// Get employee payroll information
	/// </summary>
	[HttpGet("{employeeId}/payroll")]
	[ProducesResponseType(typeof(PayrollContract), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<PayrollContract> GetAsync([FromRoute, Required] Guid employeeId)
	{
		var payrollDetails = await _employeeAppService.GetPayrollDetailsAsync(employeeId);
		return PayrollContractMapper.ToResponse(payrollDetails);
	}

	/// <summary>
	/// Update employee payroll information
	/// </summary>
	[HttpPut("{employeeId}/payroll")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] PayrollContract request)
	{
		var updateDto = PayrollContractMapper.ToUpdateDto(request);
		await _employeeAppService.UpdatePayrollDetailsAsync(employeeId, updateDto);

		return Ok();
	}
}
