using System;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class GetSalarySlipInput
{
	public Guid EmployeeId { get; set; }
	public string MonthYear { get; set; } = string.Empty;
}

public class GenerateSalarySlipInput
{
	public Guid EmployeeId { get; set; }
	public string MonthYear { get; set; } = string.Empty;
	public bool ForceRegenerate { get; set; }
}

public class UpdateSalarySlipInput
{
	public string? Remarks { get; set; }
}
