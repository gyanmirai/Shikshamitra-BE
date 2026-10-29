using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR;
using ShikshaMitra.HR.Payroll;
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

	[HttpGet("availability")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View)]
	[ProducesResponseType(typeof(SalarySlipAvailabilityDto), StatusCodes.Status200OK)]
	public Task<SalarySlipAvailabilityDto> GetAvailabilityAsync([FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear, [FromQuery] Guid? academicYearId = null) =>
		_salarySlipAppService.GetAvailabilityAsync(employeeId, monthYear, academicYearId);

	/// <summary>
	/// Retrieves employee salary slip for a specific month (OpenAPI contract)
	/// </summary>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View)]
	[ProducesResponseType(typeof(SalarySlipResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<SalarySlipResponse> GetSalarySlipAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var internalMonthYear))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		var salarySlipDto = await _salarySlipAppService.GetSalarySlipAsync(employeeId, internalMonthYear);
		return MapToSalarySlipResponse(
			salarySlipDto,
			PayrollMonthNormalizer.ToApiMonthYear(internalMonthYear) ?? internalMonthYear.ToUpperInvariant());
	}

	[HttpGet("pdf")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> GetSalarySlipPdfAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var internalMonthYear))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		var bytes = await _salarySlipAppService.GenerateSalarySlipPdfAsync(employeeId, internalMonthYear);
		var displayMonthYear = PayrollMonthNormalizer.ToApiMonthYear(internalMonthYear) ?? internalMonthYear.ToUpperInvariant();
		var fileName = $"Payslip_{employeeId:N}_{displayMonthYear}.pdf";

		return File(bytes, "application/pdf", fileName);
	}

	[HttpGet("pdf-preview")]
	[HttpGet("preview-pdf")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> GetSalarySlipPreviewPdfAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string monthYear)
	{
		if (!PayrollMonthNormalizer.TryNormalizeToInternal(monthYear, out var internalMonthYear))
		{
			throw new BusinessException(HRErrorCodes.InvalidMonthYearFormat);
		}

		var bytes = await _salarySlipAppService.GenerateSalarySlipPreviewPdfAsync(employeeId, internalMonthYear);
		var displayMonthYear = PayrollMonthNormalizer.ToApiMonthYear(internalMonthYear) ?? internalMonthYear.ToUpperInvariant();
		var fileName = $"Payslip_Preview_{employeeId:N}_{displayMonthYear}.pdf";

		return File(bytes, "application/pdf", fileName);
	}

	private static SalarySlipResponse MapToSalarySlipResponse(SalarySlipDto dto, string contractMonthYear)
	{
		return new SalarySlipResponse
		{
			DocumentId = dto.DocumentId,
			DocumentRevision = dto.DocumentRevision,
			EmployeeId = dto.EmployeeId,
			EmployeeName = dto.EmployeeName,
			EmployeeCode = dto.EmployeeCode,
			Department = dto.Department,
			Designation = dto.Designation,
			DateOfJoining = dto.DateOfJoining,
			SlipNumber = dto.SlipNumber,
			Status = dto.Status.ToString(),
			MonthYear = contractMonthYear,
			TotalEarning = Convert.ToDouble(dto.TotalEarning),
			FullGrossSalary = dto.FullGrossSalary,
			GrossSalary = dto.GrossSalary,
			AdditionalEarnings = dto.AdditionalEarnings,
			TotalDeduction = Convert.ToDouble(dto.TotalDeduction),
			NetPay = Convert.ToDouble(dto.NetSalary),
			NetSalary = Convert.ToDouble(dto.NetSalary),
			NetPayInWords = dto.NetPayInWords,
			PayrollBasisDays = dto.PayrollBasisDays,
			PayableDays = dto.PayableDays.HasValue ? Convert.ToDouble(dto.PayableDays.Value) : null,
			PresentDays = dto.PresentDays.HasValue ? Convert.ToDouble(dto.PresentDays.Value) : null,
			HalfWorkingDays = dto.HalfWorkingDays.HasValue ? Convert.ToDouble(dto.HalfWorkingDays.Value) : null,
			LopDays = dto.LopDays.HasValue ? Convert.ToDouble(dto.LopDays.Value) : null,
			ProrationFactor = dto.ProrationFactor.HasValue ? Convert.ToDouble(dto.ProrationFactor.Value) : null,
			PayrollDayCalculationMode = dto.PayrollDayCalculationMode,
			BankName = dto.BankName,
			MaskedAccountNumber = dto.MaskedAccountNumber,
			IfscCode = dto.IfscCode,
			UanNumber = dto.UanNumber,
			EpfNumber = dto.EpfNumber,
			EmployeeStateInsuranceRuleId = dto.EmployeeStateInsuranceRuleId,
			EmployeeStateInsuranceRuleVersion = dto.EmployeeStateInsuranceRuleVersion,
			EmployeeStateInsuranceWages = dto.EmployeeStateInsuranceWages,
			EmployeeStateInsuranceCoverageDecision = dto.EmployeeStateInsuranceCoverageDecision,
			EmployeeStateInsuranceNumber = dto.EmployeeStateInsuranceNumber,
			EmployeeStateInsuranceContributionPeriodStart = dto.EmployeeStateInsuranceContributionPeriodStart,
			EmployeeStateInsuranceContributionPeriodEnd = dto.EmployeeStateInsuranceContributionPeriodEnd,
			Remarks = dto.Remarks,
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
