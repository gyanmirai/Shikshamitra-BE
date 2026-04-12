using System;
using System.Collections.Generic;

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
	/// Payroll basis days (denominator) per tenant day-calculation setting: working-days, total calendar days, or manual.
	/// </summary>
	public double TotalWorkingDays { get; set; }

	/// <summary>
	/// Payable days from attendance: present +0.5 × half days.
	/// </summary>
	public double PayableDays { get; set; }

	/// <summary>
	/// LOP proration factor applied to non-temporary line items (capped at 1).
	/// </summary>
	public double ProrationFactor { get; set; }

	/// <summary>
	/// Effective day calculation mode: working-days, total-month-days, or manual-days.
	/// </summary>
	public string DayCalculationMode { get; set; } = string.Empty;

	/// <summary>
	/// True when no tenant day-calculation row exists and working-days default was used.
	/// </summary>
	public bool UsedDefaultDayCalculation { get; set; }

	/// <summary>
	/// Itemized earning components contributing to gross salary
	/// </summary>
	public List<RunPayrollComponentItem> EarningComponents { get; set; } = new();

	/// <summary>
	/// Itemized deduction components contributing to total deduction
	/// </summary>
	public List<RunPayrollComponentItem> DeductionComponents { get; set; } = new();
}

/// <summary>
/// Lightweight earning/deduction component line item in a payroll preview
/// </summary>
public class RunPayrollComponentItem
{
	public string Name { get; set; } = string.Empty;
	public double Amount { get; set; }
}

/// <summary>
/// Request to lock payroll for a month (POST /payroll/run)
/// </summary>
public class RunPayrollRequest
{
	/// <summary>
	/// Payroll month in MMMM-YYYY format (e.g., JANUARY-2025)
	/// </summary>
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

