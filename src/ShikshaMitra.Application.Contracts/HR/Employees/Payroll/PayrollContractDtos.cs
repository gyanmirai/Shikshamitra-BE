using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// PUT body for /api/employees/{employeeId}/payroll (OpenAPI Payroll schema).
/// </summary>
public class PayrollContract
{
	public BankDetailsContract? BankDetails { get; set; }
	public SalaryDetailsContract? SalaryDetails { get; set; }
	public List<SalaryComponentContract>? EarningComponents { get; set; }
	public List<SalaryComponentContract>? DeductionComponents { get; set; }
}

/// <summary>
/// GET response: employee id plus payroll payload (OpenAPI GET payroll schema).
/// </summary>
public class EmployeePayrollResponse : PayrollContract
{
	public Guid Id { get; set; }
}

/// <summary>
/// Bank details (OpenAPI BankDetails schema).
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
/// Salary / statutory details (OpenAPI SalaryDetails schema).
/// </summary>
public class SalaryDetailsContract
{
	public Guid? PfRuleId { get; set; }
	public string? EpfNumber { get; set; }
	public string? UanNumber { get; set; }
	public string? PunchId { get; set; }
	public decimal? TotalWorkingHourPerMonth { get; set; }
}

/// <summary>
/// Earning or deduction line (OpenAPI SalaryComponent schema).
/// </summary>
public class SalaryComponentContract
{
	public Guid? Id { get; set; }

	public Guid? ComponentId { get; set; }

	[Required]
	public string Name { get; set; } = string.Empty;

	public decimal? Amount { get; set; }

	public bool? IsFixed { get; set; }

	public bool? IsTemp { get; set; }
}
