using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// DTO for updating employee payroll details.
/// </summary>
public class UpdateEmployeePayrollDetailsDto
{
	public PayrollDetailsDto PayrollDetails { get; set; } = new();

	public List<PayrollComponentDto> Earnings { get; set; } = new();

	public List<PayrollComponentDto> Deductions { get; set; } = new();
}
