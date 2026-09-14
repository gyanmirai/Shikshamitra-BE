using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Request to lock payroll for a month (POST /payroll/run)
/// </summary>
public class RunPayrollRequest
{
	public Guid PreviewRunId { get; set; }
	/// <summary>
	/// Payroll month in MMMM-YYYY format (e.g., JANUARY-2025)
	/// </summary>
	public string Month { get; set; } = string.Empty;

	/// <summary>
	/// Optional academic year scope. When supplied, the payroll month must fall inside this academic year.
	/// </summary>
	public Guid? AcademicYearId { get; set; }

	/// <summary>
	/// Explicit employees selected from the reviewed preview generation.
	/// </summary>
	public List<Guid>? EmployeeIds { get; set; }
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
