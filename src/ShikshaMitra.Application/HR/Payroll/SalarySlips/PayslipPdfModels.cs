using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public sealed record PayslipPdfModel(
	string InstituteName,
	string? InstituteAddress,
	string EmployeeName,
	string EmployeeCode,
	string? Department,
	string? Designation,
	string? DateOfJoining,
	string PayPeriodDisplay,
	string SlipNumber,
	string Status,
	bool IsPreview,
	string DocumentLabel,
	DateTime GeneratedAt,
	decimal? PayrollBasisDays,
	decimal? PayableDays,
	decimal? PresentDays,
	decimal? HalfWorkingDays,
	decimal? LopDays,
	decimal? ProrationFactor,
	string? DayCalculationMode,
	string? BankName,
	string? MaskedAccountNumber,
	string? IfscCode,
	string? UanNumber,
	string? EpfNumber,
	IReadOnlyList<PayslipPdfLineItem> Earnings,
	IReadOnlyList<PayslipPdfLineItem> Deductions,
	decimal TotalEarnings,
	decimal TotalDeductions,
	decimal NetPay,
	string NetPayInWords,
	string? Remarks);

public sealed record PayslipPdfLineItem(string Name, decimal Amount);
