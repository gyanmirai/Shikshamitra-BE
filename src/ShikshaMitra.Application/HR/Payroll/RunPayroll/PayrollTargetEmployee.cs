using System;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal sealed record PayrollTargetEmployee(Guid Id, string? EmployeeCode, string? EmployeeName);
