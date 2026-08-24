using System;
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

		return new SalarySlipDto
		{
			Id = salarySlip.Id,
			EmployeeId = salarySlip.EmployeeId,
			EmployeeName = employee.EmployeeName,
			EmployeeCode = employee.EmployeeCode,
			Department = employee.Department,
			Designation = employee.Designation,
			DateOfJoining = employee.DateOfJoining,
			SlipNumber = BuildSlipNumber(employee.EmployeeCode, salarySlip.MonthYear),
			MonthYear = salarySlip.MonthYear,
			TotalEarning = salarySlip.TotalEarning,
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
			UanNumber = payrollDetails?.UanNumber,
			EpfNumber = payrollDetails?.EpfNumber,
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
			GeneratedAt: dto.ProcessedDate,
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
			Earnings: dto.Earnings.Select(x => new PayslipPdfLineItem(x.Name, x.Amount)).ToList(),
			Deductions: dto.Deductions.Select(x => new PayslipPdfLineItem(x.Name, x.Amount)).ToList(),
			TotalEarnings: dto.TotalEarning,
			TotalDeductions: dto.TotalDeduction,
			NetPay: dto.NetSalary,
			NetPayInWords: dto.NetPayInWords ?? IndianCurrencyWords.Format(dto.NetSalary),
			Remarks: dto.Remarks);
	}

	private static SalarySlipLineItemDto ToLineItemDto(SalarySlipLineItem lineItem)
	{
		return new SalarySlipLineItemDto
		{
			Name = lineItem.Name,
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
