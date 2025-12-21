using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// API Controller for salary slip operations
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/salary-slip")]
[Authorize]
public class SalarySlipController : AbpControllerBase
{
	private readonly ISalarySlipAppService _salarySlipAppService;

	public SalarySlipController(ISalarySlipAppService salarySlipAppService)
	{
		_salarySlipAppService = salarySlipAppService;
	}

	/// <summary>
	/// Fetch salary slip earning and deduction details
	/// </summary>
	/// <param name="employeeId">Employee identifier (required)</param>
	/// <param name="monthYear">Month and year in YYYY-MM format (required, e.g., "2025-01")</param>
	/// <returns>Salary slip details with earnings and deductions breakdown</returns>
	/// <response code="200">Salary slip details fetched successfully</response>
	/// <response code="400">Invalid or missing query parameters</response>
	/// <response code="404">Employee or salary slip not found</response>
	/// <response code="500">Server error</response>
	[HttpGet]
	[ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<SalarySlipDto> GetAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		return await _salarySlipAppService.GetSalarySlipAsync(employeeId, monthYear);
	}
}
