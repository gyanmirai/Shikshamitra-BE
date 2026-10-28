using System;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>Read-only workflow status. Contains no draft payroll or monetary evidence.</summary>
public record SalarySlipAvailabilityDto(Guid EmployeeId, string MonthYear, string Status,
	string Message, bool CanViewPayslip);
