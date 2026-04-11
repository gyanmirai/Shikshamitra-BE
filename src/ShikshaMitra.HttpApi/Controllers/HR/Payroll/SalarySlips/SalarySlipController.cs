using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR;
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
	[HttpGet]
	[ProducesResponseType(typeof(SalarySlipResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<SalarySlipResponse> GetSalarySlipAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		if (!System.Text.RegularExpressions.Regex.IsMatch(monthYear, @"^[A-Z]+-\d{4}$"))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		var internalMonthYear = ConvertToInternalFormat(monthYear);
		var salarySlipDto = await _salarySlipAppService.GetSalarySlipAsync(employeeId, internalMonthYear);
		return MapToSalarySlipResponse(salarySlipDto, monthYear);
	}

	private static string ConvertToInternalFormat(string monthYear)
	{
		var parts = monthYear.Split('-');
		if (parts.Length != 2)
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		if (DateTime.TryParseExact(parts[0], "MMMM", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var date))
		{
			return $"{parts[1]}-{date.Month:D2}";
		}

		throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
	}

	private static SalarySlipResponse MapToSalarySlipResponse(SalarySlipDto dto, string contractMonthYear)
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
