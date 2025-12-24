using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.PayrollInfo;

/// <summary>
/// Request/Response DTO for employee payroll information
/// </summary>
public class PayrollContract
{
	public BankDetailsInfo? BankDetails { get; set; }

	public SalaryDetailsInfo? SalaryDetails { get; set; }

	public List<ComponentAmountInfo> EarningComponents { get; set; } = new();

	public List<ComponentAmountInfo> DeductionComponents { get; set; } = new();
}

/// <summary>
/// Bank account details
/// </summary>
public class BankDetailsInfo
{
	[MaxLength(256)]
	public string? BankName { get; set; }

	[MaxLength(256)]
	public string? BranchName { get; set; }

	[MaxLength(256)]
	public string? AccountHolderName { get; set; }

	[MaxLength(50)]
	public string? AccountNumber { get; set; }

	[MaxLength(11)]
	public string? IfscCode { get; set; }
}

/// <summary>
/// Salary configuration details
/// </summary>
public class SalaryDetailsInfo
{
	public Guid? PfRuleId { get; set; }

	[MaxLength(50)]
	public string? EpfNumber { get; set; }

	[MaxLength(50)]
	public string? UanNumber { get; set; }

	public decimal? BasicSalary { get; set; }

	[MaxLength(50)]
	public string? PunchId { get; set; }

	public decimal? TdsPercentage { get; set; }

	public decimal? TdsAmount { get; set; }
}

/// <summary>
/// Component amount (earning or deduction)
/// </summary>
public class ComponentAmountInfo
{
	[Required]
	public Guid ComponentId { get; set; }

	[Required]
	public decimal Amount { get; set; }
}
