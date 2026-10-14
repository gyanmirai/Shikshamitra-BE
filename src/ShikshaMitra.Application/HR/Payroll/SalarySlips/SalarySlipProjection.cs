using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

internal static class SalarySlipProjection
{
	public static SalarySlipDto ToDto(
		SalarySlip salarySlip,
		Employee employee,
		PayrollRunItemSnapshot? snapshot,
		EmployeePayrollDetails? payrollDetails)
	{
		var payrollBasisDays = salarySlip.PayrollBasisDays ?? snapshot?.TotalWorkingDays;
		var payableDays = salarySlip.PayableDays ?? snapshot?.PayableDays;
		var lopDays = payrollBasisDays.HasValue && payableDays.HasValue
			? Math.Max(0m, payrollBasisDays.Value - payableDays.Value)
			: (decimal?) null;

		var application = ReadApplication(snapshot?.StatutoryApplicationJson ?? salarySlip.StatutoryApplicationJson);
		return new SalarySlipDto
		{
			Id = salarySlip.Id,
			EmployeeId = salarySlip.EmployeeId,
			BasicSalary = snapshot?.BasicSalary,
			StatutoryDeductionAmounts = snapshot?.StatutoryApplicationJson == null ? null : ReadStatutoryDeductions(snapshot.DeductionComponentsJson),
			LeaveUnitsByType = snapshot?.LeaveUnitsByTypeJson == null ? null : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, decimal>>(snapshot.LeaveUnitsByTypeJson),
			StatutoryApplication = ReadApplication(snapshot?.StatutoryApplicationJson ?? salarySlip.StatutoryApplicationJson),
			EmployeeName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			Department = employee.Department,
			Designation = employee.Designation,
			DateOfJoining = employee.DateOfJoining,
			SlipNumber = BuildSlipNumber(employee.EmployeeCode, salarySlip.MonthYear),
			MonthYear = salarySlip.MonthYear,
			TotalEarning = salarySlip.TotalEarning,
			FullGrossSalary = salarySlip.Earnings.Where(x => !x.IsTemp).All(x => x.FullMonthAmount.HasValue)
				? salarySlip.Earnings.Where(x => !x.IsTemp).Sum(x => x.FullMonthAmount) : snapshot?.FullGrossSalary,
			GrossSalary = salarySlip.Earnings.Where(x => !x.IsTemp).Sum(x => x.Amount),
			AdditionalEarnings = salarySlip.Earnings.Where(x => x.IsTemp).Sum(x => x.Amount),
			TotalDeduction = salarySlip.TotalDeduction,
			NetSalary = salarySlip.NetSalary,
			Status = salarySlip.Status,
			Remarks = salarySlip.Remarks,
			ProcessedDate = salarySlip.ProcessedDate,
			PayrollBasisDays = payrollBasisDays,
			PayableDays = payableDays,
			PresentDays = snapshot?.PresentDays,
			HalfWorkingDays = snapshot?.HalfWorkingDays,
			LopDays = lopDays,
			ProrationFactor = salarySlip.ProrationFactor ?? snapshot?.ProrationFactor,
			PayrollDayCalculationMode = salarySlip.PayrollDayCalculationMode.HasValue
				? PayrollDayCalculationApiLabels.ToApiString(salarySlip.PayrollDayCalculationMode.Value)
				: snapshot?.DayCalculationMode,
			BankName = payrollDetails?.BankName,
			MaskedAccountNumber = SalarySlipProjectionFormatting.MaskAccountNumber(payrollDetails?.AccountNumber),
			IfscCode = payrollDetails?.IfscCode,
			UanNumber = application == null ? salarySlip.Status == SalarySlipStatus.Draft ? payrollDetails?.UanNumber : null : application.Profile.UanNumber,
			EpfNumber = application == null ? salarySlip.Status == SalarySlipStatus.Draft ? payrollDetails?.EpfNumber : null : application.Profile.PfNumber,
			NetPayInWords = IndianCurrencyWords.Format(salarySlip.NetSalary),
			Earnings = salarySlip.Earnings.Select(ToLineItemDto).ToList(),
			Deductions = salarySlip.Deductions.Select(ToLineItemDto).ToList(),
			EmployerContributions = salarySlip.EmployerContributions.Select(ToLineItemDto).ToList(),
			EmployerContributionTotal = salarySlip.EmployerContributionTotal,
			ProvidentFundRuleId = salarySlip.ProvidentFundRuleId,
			ProvidentFundRuleVersion = salarySlip.ProvidentFundRuleVersion,
			ProfessionalTaxRuleId = salarySlip.ProfessionalTaxRuleId,
			ProfessionalTaxRuleVersion = salarySlip.ProfessionalTaxRuleVersion,
			EmployeeStateInsuranceRuleId = salarySlip.EmployeeStateInsuranceRuleId,
			EsiConfigurationId = salarySlip.EsiConfigurationId,
			EmployeeStateInsuranceRuleVersion = salarySlip.EmployeeStateInsuranceRuleVersion,
			EmployeeStateInsuranceWages = salarySlip.EmployeeStateInsuranceWages,
			EmployeeStateInsuranceGovernmentContribution = salarySlip.EmployeeStateInsuranceGovernmentContribution,
			EmployeeStateInsuranceCoverageDecision = salarySlip.EmployeeStateInsuranceCoverageDecision,
			EmployeeStateInsuranceNumber = salarySlip.EmployeeStateInsuranceNumber,
			EmployeeStateInsuranceContributionPeriodStart = salarySlip.EmployeeStateInsuranceContributionPeriodStart,
			EmployeeStateInsuranceContributionPeriodEnd = salarySlip.EmployeeStateInsuranceContributionPeriodEnd,
			CompensationRevisionId = salarySlip.CompensationRevisionId,
			CompensationRevisionNumber = salarySlip.CompensationRevisionNumber,
			CompensationSalaryBasis = salarySlip.CompensationSalaryBasis,
			CompensationProtectionPolicy = salarySlip.CompensationProtectionPolicy,
			CompensationQuotedAmount = salarySlip.CompensationQuotedAmount,
			CompensationProtectedAmount = salarySlip.CompensationProtectedAmount,
			CompensationNormalizedProtectedAmount = salarySlip.CompensationNormalizedProtectedAmount,
			CompensationTargetDifference = salarySlip.CompensationTargetDifference
		};
	}

	public static PayslipPdfModel ToPdfModel(SalarySlipDto dto, string instituteName, bool isPreview = false)
	{
		var statusLabel = dto.Status.ToString();

		return new PayslipPdfModel(
			InstituteName: instituteName,
			InstituteAddress: null,
			EmployeeName: dto.EmployeeName ?? "-",
			EmployeeCode: dto.EmployeeCode ?? "-",
			Department: dto.Department,
			Designation: dto.Designation,
			DateOfJoining: SalarySlipProjectionFormatting.FormatDate(dto.DateOfJoining),
			PayPeriodDisplay: SalarySlipProjectionFormatting.FormatPayPeriod(dto.MonthYear),
			SlipNumber: dto.SlipNumber ?? BuildSlipNumber(dto.EmployeeCode, dto.MonthYear),
			Status: statusLabel,
			IsPreview: isPreview,
			DocumentLabel: isPreview ? $"Preview - {statusLabel}" : statusLabel,
			GeneratedAt: TimeZoneInfo.ConvertTimeFromUtc(dto.ProcessedDate.Kind == DateTimeKind.Local
				? dto.ProcessedDate.ToUniversalTime() : DateTime.SpecifyKind(dto.ProcessedDate, DateTimeKind.Utc),
				TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")),
			PayrollBasisDays: dto.PayrollBasisDays,
			PayableDays: dto.PayableDays,
			PresentDays: dto.PresentDays,
			HalfWorkingDays: dto.HalfWorkingDays,
			LopDays: dto.LopDays,
			ProrationFactor: dto.ProrationFactor,
			DayCalculationMode: SalarySlipProjectionFormatting.FormatDayCalculationMode(dto.PayrollDayCalculationMode),
			BankName: dto.BankName,
			MaskedAccountNumber: dto.MaskedAccountNumber,
			IfscCode: dto.IfscCode,
			UanNumber: dto.UanNumber,
			EpfNumber: dto.EpfNumber,
			Earnings: dto.Earnings.Select(x => new PayslipPdfLineItem(
				PayslipPdfFormatting.FormatEmployeeComponentName(x.Name), x.Amount, x.FullMonthAmount)).ToList(),
			Deductions: dto.Deductions.Select(x => new PayslipPdfLineItem(
				PayslipPdfFormatting.FormatEmployeeComponentName(x.Name), x.Amount)).ToList(),
			TotalEarnings: dto.TotalEarning,
			TotalDeductions: dto.TotalDeduction,
			NetPay: dto.NetSalary,
			NetPayInWords: IndianCurrencyWords.Format(dto.NetSalary),
			Remarks: dto.Remarks)
		{
			Presentation = dto.Presentation?.Options ?? new(),
			CasualLeaveDays = dto.LeaveUnitsByType?.GetValueOrDefault("CL"), UnpaidLeaveDays = dto.LeaveUnitsByType?.GetValueOrDefault("UNPAID"),
			FullGrossSalary = dto.FullGrossSalary,
			GrossSalary = dto.GrossSalary,
			AdditionalEarnings = dto.AdditionalEarnings,
			InsuranceNumber = dto.EmployeeStateInsuranceRuleId.HasValue ? dto.EmployeeStateInsuranceNumber : null
		};
	}

	internal static Dictionary<string, decimal> ReadStatutoryDeductions(string json)
	{
		var lines = System.Text.Json.JsonSerializer.Deserialize<List<RunPayrollComponentItem>>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
		return new() { ["pt"] = lines.Where(x => string.Equals(x.SemanticType?.Replace("_", ""), "ProfessionalTax", StringComparison.OrdinalIgnoreCase)).Sum(x => (decimal)x.Amount),
			["tds"] = lines.Where(x => new[] { "TDS", "T.D.S.", "TAXDEDUCTEDATSOURCE" }.Contains(x.Name.Replace(" ", "").ToUpperInvariant())).Sum(x => (decimal)x.Amount) };
	}
	private static ShikshaMitra.HR.Payroll.Statutory.PayrollStatutoryApplication? ReadApplication(string? json) =>
		json == null ? null : System.Text.Json.JsonSerializer.Deserialize<ShikshaMitra.HR.Payroll.Statutory.PayrollStatutoryApplication>(json);

	private static SalarySlipLineItemDto ToLineItemDto(SalarySlipLineItem lineItem)
	{
		return new SalarySlipLineItemDto
		{
			Name = lineItem.Name,
			FullMonthAmount = lineItem.FullMonthAmount,
			Amount = lineItem.Amount,
			Description = lineItem.Description,
			IsFixed = lineItem.IsFixed,
			IsTemp = lineItem.IsTemp,
			ComponentType = lineItem.ComponentType.ToString(),
			DependOn = lineItem.DependOn.ToString()
		};
	}

	private static string BuildSlipNumber(string? employeeCode, string monthYear)
	{
		var normalizedCode = string.IsNullOrWhiteSpace(employeeCode) ? "EMP" : employeeCode.Trim();
		return $"PS-{monthYear.Replace("-", string.Empty, StringComparison.Ordinal)}-{normalizedCode}";
	}
}
