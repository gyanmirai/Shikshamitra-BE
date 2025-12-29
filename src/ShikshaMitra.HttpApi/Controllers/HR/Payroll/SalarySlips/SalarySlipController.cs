using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Payroll.SalarySlips;

/// <summary>
/// API Controller for salary slip operations (OpenAPI contract compliant)
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/payroll/salary-slips")]
[Authorize]
public class SalarySlipController : AbpControllerBase
{
	private readonly ISalarySlipAppService _salarySlipAppService;

	public SalarySlipController(ISalarySlipAppService salarySlipAppService)
	{
		_salarySlipAppService = salarySlipAppService;
	}

	/// <summary>
	/// Retrieves employee salary slip for a specific month (OpenAPI contract)
	/// </summary>
	/// <param name="employeeId">Unique employee identifier (required)</param>
	/// <param name="monthYear">Salary period in MMMM-YYYY format (required, e.g., "JANUARY-2025")</param>
	/// <returns>Salary slip with earnings and deductions breakdown</returns>
	/// <response code="200">Salary slip retrieved successfully</response>
	/// <response code="400">Invalid or missing query parameters</response>
	/// <response code="404">Employee or salary slip not found</response>
	[HttpGet]
	[ProducesResponseType(typeof(SalarySlipResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<SalarySlipResponse> GetSalarySlipAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		// Validate monthYear format (MMMM-YYYY)
		if (!System.Text.RegularExpressions.Regex.IsMatch(monthYear, @"^[A-Z]+-\d{4}$"))
		{
			throw new UserFriendlyException(
				"Invalid monthYear format. Expected format: MMMM-YYYY (e.g., JANUARY-2025)");
		}

		// Convert from OpenAPI format (JANUARY-2025) to internal format (2025-01)
		var internalMonthYear = ConvertToInternalFormat(monthYear);

		// Fetch salary slip using internal format
		var salarySlipDto = await _salarySlipAppService.GetSalarySlipAsync(employeeId, internalMonthYear);

		// Convert to OpenAPI contract format
		return MapToSalarySlipResponse(salarySlipDto, monthYear);
	}

	/// <summary>
	/// Converts MMMM-YYYY format to YYYY-MM format for internal use
	/// </summary>
	private string ConvertToInternalFormat(string monthYear)
	{
		try
		{
			var parts = monthYear.Split('-');
			if (parts.Length != 2)
			{
				throw new UserFriendlyException("Invalid monthYear format");
			}

			var monthName = parts[0];
			var year = parts[1];

			if (DateTime.TryParseExact(monthName, "MMMM", CultureInfo.InvariantCulture, 
				DateTimeStyles.None, out var date))
			{
				return $"{year}-{date.Month:D2}";
			}

			throw new UserFriendlyException("Invalid month name in monthYear");
		}
		catch
		{
			throw new UserFriendlyException(
				"Invalid monthYear format. Expected format: MMMM-YYYY (e.g., JANUARY-2025)");
		}
	}

	/// <summary>
	/// Maps internal SalarySlipDto to OpenAPI contract SalarySlipResponse
	/// </summary>
	private SalarySlipResponse MapToSalarySlipResponse(SalarySlipDto dto, string contractMonthYear)
	{
		return new SalarySlipResponse
		{
			EmployeeId = dto.EmployeeId,
			MonthYear = contractMonthYear,
			TotalEarning = Convert.ToDouble(dto.TotalEarning),
			TotalDeduction = Convert.ToDouble(dto.TotalDeduction),
			NetPay = Convert.ToDouble(dto.NetSalary),
			Earnings = dto.Earnings.Select(e => new SalaryComponent
			{
				Name = e.Name,
				Description = e.Description,
				Amount = Convert.ToDouble(e.Amount),
				IsFixed = e.IsFixed,
				IsTemp = e.IsTemp,
				ComponentType = e.ComponentType.ToString().ToUpperInvariant(),
				DependOn = e.DependOn != "None" ? e.DependOn.ToUpperInvariant() : null
			}).ToList(),
			Deductions = dto.Deductions.Select(d => new SalaryComponent
			{
				Name = d.Name,
				Description = d.Description,
				Amount = Convert.ToDouble(d.Amount),
				IsFixed = d.IsFixed,
				IsTemp = d.IsTemp,
				ComponentType = d.ComponentType.ToString().ToUpperInvariant(),
				DependOn = d.DependOn != "None" ? d.DependOn.ToUpperInvariant() : null
			}).ToList(),
			GeneratedAt = dto.ProcessedDate
		};
	}
}
