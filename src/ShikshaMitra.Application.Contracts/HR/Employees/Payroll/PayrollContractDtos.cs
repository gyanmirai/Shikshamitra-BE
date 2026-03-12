using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Main payroll response/request (matches OpenAPI Payroll schema)
/// </summary>
public class PayrollContract
{
	public BankDetailsContract? BankDetails { get; set; }
	public SalaryDetailsContract? SalaryDetails { get; set; }
	public List<ComponentAmountContract>? EarningComponents { get; set; }
	public List<ComponentAmountContract>? DeductionComponents { get; set; }
}

/// <summary>
/// Bank details (matches OpenAPI BankDetails schema)
/// </summary>
public class BankDetailsContract
{
	public string? BankName { get; set; }
	public string? BranchName { get; set; }
	public string? AccountHolderName { get; set; }
	public string? AccountNumber { get; set; }
	public string? IfscCode { get; set; }
}

/// <summary>
/// Salary details (matches OpenAPI SalaryDetails schema)
/// </summary>
public class SalaryDetailsContract
{
	/// <summary>
	/// Provident Fund rule ID
	/// </summary>
	public Guid? PfRuleId { get; set; }

	/// <summary>
	/// Employee Provident Fund number
	/// </summary>
	public string? EpfNumber { get; set; }

	/// <summary>
	/// Universal Account Number
	/// </summary>
	public string? UanNumber { get; set; }

	/// <summary>
	/// Basic salary amount
	/// </summary>
	public decimal? BasicSalary { get; set; }

	/// <summary>
	/// Punch/Attendance ID
	/// </summary>
	public string? PunchId { get; set; }

	/// <summary>
	/// TDS percentage (0-100)
	/// </summary>
	[Range(0, 100)]
	public decimal? TdsPercentage { get; set; }

	/// <summary>
	/// Calculated TDS amount
	/// </summary>
	public decimal? TdsAmount { get; set; }
}

/// <summary>
/// Component with amount (matches OpenAPI ComponentAmount schema)
/// </summary>
public class ComponentAmountContract
{
	/// <summary>
	/// ID of the earning/deduction component
	/// </summary>
	[Required]
	public Guid ComponentId { get; set; }

	/// <summary>
	/// Amount for this component
	/// </summary>
	[Required]
	[Range(0, double.MaxValue)]
	public decimal Amount { get; set; }
}

