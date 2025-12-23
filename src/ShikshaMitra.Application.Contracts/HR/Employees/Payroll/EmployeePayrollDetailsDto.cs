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
/// Matches the OpenAPI SalaryComponent schema.
/// </summary>
public class PayrollComponentDto
{
	/// <summary>
	/// Component name (e.g., Basic, PF, Tax).
	/// </summary>
	public string Name { get; set; } = string.Empty;
	
	/// <summary>
	/// Optional explanation of the component.
	/// </summary>
	public string? Description { get; set; }
	
	/// <summary>
	/// Final calculated amount.
	/// </summary>
	public decimal Amount { get; set; }
	
	/// <summary>
	/// Fixed for all employees or employee-specific.
	/// </summary>
	public bool IsFixed { get; set; }
	
	/// <summary>
	/// Temporary or one-time component.
	/// </summary>
	public bool IsTemp { get; set; }
	
	/// <summary>
	/// Calculation strategy (Fixed or Percentage).
	/// </summary>
	public SalaryComponentType ComponentType { get; set; }
	
	/// <summary>
	/// Required only when componentType is Percentage.
	/// Indicates what the percentage is based on (Basic or Gross).
	/// </summary>
	public SalaryComponentDependency? DependOn { get; set; }
}



