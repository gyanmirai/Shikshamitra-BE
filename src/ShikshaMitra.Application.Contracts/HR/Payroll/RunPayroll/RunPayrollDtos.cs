using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Response for payroll preview (GET /payroll/run)
/// </summary>
public class RunPayrollResponse
{
	/// <summary>
	/// Indicates whether payroll is already generated (locked) for the month
	/// </summary>
	public bool IsGenerated { get; set; }
	
	/// <summary>
	/// Total number of employees
	/// </summary>
	public int TotalCount { get; set; }
	
	/// <summary>
	/// Current page index (0-based)
	/// </summary>
	public int CurrentPage { get; set; }
	
	/// <summary>
	/// Page size
	/// </summary>
	public int PageSize { get; set; }
	
	/// <summary>
	/// Total number of pages
	/// </summary>
	public int TotalPages { get; set; }
	
	/// <summary>
	/// Payroll items for employees
	/// </summary>
	public List<RunPayrollItem> Items { get; set; } = new();
}

/// <summary>
/// Individual employee payroll item
/// </summary>
public class RunPayrollItem
{
	/// <summary>
	/// Employee full name
	/// </summary>
	public string EmployeeName { get; set; } = string.Empty;
	
	/// <summary>
	/// Employee code/identifier
	/// </summary>
	public string EmployeeIdentifier { get; set; } = string.Empty;
	
	/// <summary>
	/// Whether employee is teaching staff
	/// </summary>
	public bool IsTeaching { get; set; }
	
	/// <summary>
	/// Basic salary amount
	/// </summary>
	public double BasicSalary { get; set; }
	
	/// <summary>
	/// Gross salary (sum of all earnings)
	/// </summary>
	public double GrossSalary { get; set; }
	
	/// <summary>
	/// Total deductions
	/// </summary>
	public double TotalDeduction { get; set; }
	
	/// <summary>
	/// Net pay (Gross - Deductions)
	/// </summary>
	public double NetPay { get; set; }
	
	/// <summary>
	/// Number of present days in the month
	/// </summary>
	public double PresentDays { get; set; }
	
	/// <summary>
	/// Number of half working days (half-day leaves)
	/// </summary>
	public double HalfWorkingDays { get; set; }
	
	/// <summary>
	/// Total working days in the month (business days excluding holidays)
	/// </summary>
	public double TotalWorkingDays { get; set; }
}

/// <summary>
/// Request to lock payroll for a month (POST /payroll/run)
/// </summary>
public class RunPayrollRequest
{
	/// <summary>
	/// Payroll month in MMMM-YYYY format (e.g., JANUARY-2025)
	/// </summary>
	[Required]
	[RegularExpression(@"^[A-Z]+-\d{4}$", ErrorMessage = "Month must be in MMMM-YYYY format (e.g., JANUARY-2025)")]
	public string Month { get; set; } = string.Empty;
}

/// <summary>
/// Response after locking payroll
/// </summary>
public class PayrollLockResponse
{
	/// <summary>
	/// Success or status message
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Month that was locked
	/// </summary>
	public string Month { get; set; } = string.Empty;

	/// <summary>
	/// Indicates if payroll was already locked (idempotent response)
	/// </summary>
	public bool WasAlreadyLocked { get; set; }

	/// <summary>
	/// Number of employees successfully processed
	/// </summary>
	public int EmployeesProcessed { get; set; }

	/// <summary>
	/// Number of employees skipped (already finalized)
	/// </summary>
	public int EmployeesSkipped { get; set; }

	/// <summary>
	/// Number of employees that failed processing
	/// </summary>
	public int EmployeesFailed { get; set; }

	/// <summary>
	/// Optional list of error messages for failed employees
	/// </summary>
	public List<string>? Errors { get; set; }
}

/// <summary>
/// Input for getting payroll preview
/// </summary>
public class GetRunPayrollInput
{
	/// <summary>
	/// Month in MMMM-YYYY format
	/// </summary>
	public string Month { get; set; } = string.Empty;
	
	/// <summary>
	/// Page index (0-based)
	/// </summary>
	public int Page { get; set; }
	
	/// <summary>
	/// Page size
	/// </summary>
	public int PageSize { get; set; } = 50;
}

