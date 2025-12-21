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
	/// Get salary slip by employee and month-year
	/// </summary>
	/// <param name="employeeId">Employee identifier (required)</param>
	/// <param name="monthYear">Month and year in YYYY-MM format (required, e.g., "2025-01")</param>
	/// <returns>Salary slip details with earnings and deductions breakdown</returns>
	/// <response code="200">Salary slip details fetched successfully</response>
	/// <response code="400">Invalid or missing query parameters</response>
	/// <response code="404">Employee or salary slip not found</response>
	/// <response code="500">Server error</response>
	[HttpGet("by-employee")]
	[ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<SalarySlipDto> GetByEmployeeAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		return await _salarySlipAppService.GetSalarySlipAsync(employeeId, monthYear);
	}

	/// <summary>
	/// Get salary slip by identifier
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <returns>Salary slip details</returns>
	/// <response code="200">Salary slip retrieved successfully</response>
	/// <response code="404">Salary slip not found</response>
	/// <response code="500">Server error</response>
	[HttpGet("{id}")]
	[ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<SalarySlipDto> GetAsync(Guid id)
	{
		return await _salarySlipAppService.GetAsync(id);
	}

	/// <summary>
	/// Generate a new salary slip or regenerate an existing draft
	/// </summary>
	/// <param name="input">Generate salary slip input parameters</param>
	/// <returns>Generated salary slip details</returns>
	/// <response code="200">Salary slip generated successfully</response>
	/// <response code="400">Invalid input parameters or cannot regenerate finalized slip</response>
	/// <response code="404">Employee not found</response>
	/// <response code="500">Server error</response>
	[HttpPost("generate")]
	[ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<SalarySlipDto> GenerateAsync([FromBody] GenerateSalarySlipInput input)
	{
		return await _salarySlipAppService.GenerateSalarySlipAsync(input);
	}

	/// <summary>
	/// Update a draft salary slip (remarks only)
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <param name="input">Update input containing new remarks</param>
	/// <returns>Updated salary slip details</returns>
	/// <response code="200">Salary slip updated successfully</response>
	/// <response code="400">Cannot update finalized salary slip</response>
	/// <response code="404">Salary slip not found</response>
	/// <response code="500">Server error</response>
	[HttpPut("{id}")]
	[ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<SalarySlipDto> UpdateAsync(Guid id, [FromBody] UpdateSalarySlipInput input)
	{
		return await _salarySlipAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Finalize a salary slip, making it immutable
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <returns>Finalized salary slip details</returns>
	/// <response code="200">Salary slip finalized successfully</response>
	/// <response code="400">Salary slip is already finalized</response>
	/// <response code="404">Salary slip not found</response>
	/// <response code="500">Server error</response>
	[HttpPost("{id}/finalize")]
	[ProducesResponseType(typeof(SalarySlipDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<SalarySlipDto> FinalizeAsync(Guid id)
	{
		return await _salarySlipAppService.FinalizeAsync(id);
	}

	/// <summary>
	/// Delete a draft salary slip
	/// </summary>
	/// <param name="id">Salary slip identifier</param>
	/// <response code="204">Salary slip deleted successfully</response>
	/// <response code="400">Cannot delete finalized salary slip</response>
	/// <response code="404">Salary slip not found</response>
	/// <response code="500">Server error</response>
	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task DeleteAsync(Guid id)
	{
		await _salarySlipAppService.DeleteAsync(id);
	}
}
