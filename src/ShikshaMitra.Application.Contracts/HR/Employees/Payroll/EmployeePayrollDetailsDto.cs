using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// DTO for employee payroll details including bank info, earnings, and deductions.
/// </summary>
public class EmployeePayrollDetailsDto
{
	public string EmployeeId { get; set; } = string.Empty;
	
	public PayrollDetailsDto PayrollDetails { get; set; } = new();
	
	public List<PayrollComponentDto> Earnings { get; set; } = new();
	
	public List<PayrollComponentDto> Deductions { get; set; } = new();
}

/// <summary>
/// DTO for basic payroll configuration details.
/// </summary>
public class PayrollDetailsDto
{
	public string? BankName { get; set; }
	
	public string? BranchName { get; set; }
	
	public string? AccountHolderName { get; set; }
	
	public string? AccountNumber { get; set; }
	
	public string? IfscCode { get; set; }
	
	public string? PfRule { get; set; }
	
	public string? EpfNumber { get; set; }
	
	public string? UanNumber { get; set; }
	
	public string? PunchId { get; set; }
	
	public decimal? BasicSalary { get; set; }
	
	public decimal? TdsPercentage { get; set; }
	
	public decimal? TdsAmount { get; set; }
}

/// <summary>
/// DTO for individual payroll components (earnings or deductions).
/// </summary>
public class PayrollComponentDto
{
	public string ComponentName { get; set; } = string.Empty;
	
	public PayrollComponentType Type { get; set; }
	
	public decimal Amount { get; set; }
}



