using System;
using System.Collections.Generic;
using FluentValidation;

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

	/// <summary>
	/// Month-level readiness summary for the payroll workbench.
	/// </summary>
	public RunPayrollReadinessSummary Readiness { get; set; } = new();
}

public class RunPayrollReadinessSummary
{
	public string Month { get; set; } = string.Empty;
	public bool CanPreview { get; set; }
	public bool CanLock { get; set; }
	public int EmployeeCount { get; set; }
	public int SnapshotCount { get; set; }
	public int ReviewedCount { get; set; }
	public int LockedCount { get; set; }
	public int AttendanceFoundCount { get; set; }
	public int MissingAttendanceCount { get; set; }
	public int MissingSalaryStructureCount { get; set; }
	public int PolicyConfigurationIssueCount { get; set; }
	public List<string> BlockingIssues { get; set; } = new();
	public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Individual employee payroll item
/// </summary>
public class RunPayrollItem
{
	/// <summary>
	/// Employee identifier (UUID)
	/// </summary>
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// Indicates whether payroll is already finalized/locked for this employee in the selected month.
	/// </summary>
	public bool IsLocked { get; set; }

	/// <summary>
	/// Review state for lock gating: PENDING, REVIEWED, or LOCKED.
	/// </summary>
	public string ReviewStatus { get; set; } = "PENDING";

	/// <summary>
	/// User who most recently marked this payroll row reviewed.
	/// </summary>
	public Guid? ReviewedByUserId { get; set; }

	/// <summary>
	/// Timestamp when this payroll row was most recently reviewed.
	/// </summary>
	public DateTime? ReviewedAt { get; set; }

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
	/// Full gross salary before attendance proration.
	/// </summary>
	public double FullGrossSalary { get; set; }

	/// <summary>
	/// Total deductions
	/// </summary>
	public double TotalDeduction { get; set; }

	/// <summary>
	/// Net pay (Gross - Deductions)
	/// </summary>
	public double NetPay { get; set; }

	/// <summary>
	/// Finalized net pay from the previous month, if available.
	/// </summary>
	public double? PreviousMonthNetPay { get; set; }

	/// <summary>
	/// Current net pay minus previous month finalized net pay.
	/// </summary>
	public double? NetPayDelta { get; set; }

	/// <summary>
	/// Net pay variance percentage compared with previous month.
	/// </summary>
	public double? NetPayDeltaPercent { get; set; }

	/// <summary>
	/// Deterministic issue codes detected for fast payroll review.
	/// </summary>
	public List<string> IssueCodes { get; set; } = new();

	/// <summary>
	/// Number of present days in the month
	/// </summary>
	public double PresentDays { get; set; }

	/// <summary>
	/// Number of half working days (half-day leaves)
	/// </summary>
	public double HalfWorkingDays { get; set; }

	/// <summary>
	/// Payroll basis days (denominator) from the resolved payroll policy: working-days, total calendar days, or manual.
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
	/// True when a fallback policy default was used.
	/// </summary>
	public bool UsedDefaultDayCalculation { get; set; }

	public Guid? PayrollPolicyId { get; set; }
	public int? PayrollPolicyVersion { get; set; }
	public Guid? AttendancePolicyId { get; set; }
	public int? AttendancePolicyVersion { get; set; }
	public Guid? WorkCalendarPolicyId { get; set; }
	public int? WorkCalendarPolicyVersion { get; set; }
	public string HolidayImpactJson { get; set; } = "[]";
	public double BasisDays { get; set; }
	public double PayableUnits { get; set; }
	public string PayrollAttendanceSource { get; set; } = "raw_attendance";
	public List<string> AttendancePolicyWarnings { get; set; } = new();

	/// <summary>
	/// Itemized earning components contributing to gross salary
	/// </summary>
	public List<RunPayrollComponentItem> EarningComponents { get; set; } = new();

	/// <summary>
	/// Itemized deduction components contributing to total deduction
	/// </summary>
	public List<RunPayrollComponentItem> DeductionComponents { get; set; } = new();

	/// <summary>
	/// Payroll-only financial adjustments included in this preview/lock.
	/// </summary>
	public List<PayrollAdjustmentDto> PayrollAdjustments { get; set; } = new();
}

/// <summary>
/// Lightweight earning/deduction component line item in a payroll preview
/// </summary>
public class RunPayrollComponentItem
{
	public string Name { get; set; } = string.Empty;
	public double Amount { get; set; }
	public string? Description { get; set; }
	public bool IsFixed { get; set; } = true;
	public bool IsTemp { get; set; }
	public string ComponentType { get; set; } = "Fixed";
	public string DependOn { get; set; } = "None";
}

public class PayrollAdjustmentDto
{
	public Guid Id { get; set; }
	public string MonthYear { get; set; } = string.Empty;
	public Guid EmployeeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public double Amount { get; set; }
	public bool IsEarning { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? Description { get; set; }
	public DateTime? CreationTime { get; set; }
	public Guid? CreatorId { get; set; }
}

public class PayrollAdjustmentRequest
{
	public string Month { get; set; } = string.Empty;
	public Guid? AcademicYearId { get; set; }
	public Guid EmployeeId { get; set; }
	public string Name { get; set; } = string.Empty;
	public double Amount { get; set; }
	public bool IsEarning { get; set; }
	public string Reason { get; set; } = string.Empty;
	public string? Description { get; set; }
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

	/// <summary>
	/// Optional academic year scope. When supplied, the payroll month must fall inside this academic year.
	/// </summary>
	public Guid? AcademicYearId { get; set; }

	/// <summary>
	/// Optional target employee IDs to finalize a subset. If omitted, payroll runs for all in-scope active employees.
	/// </summary>
	public List<Guid>? EmployeeIds { get; set; }
}

/// <summary>
/// Request to mark or unmark monthly payroll review rows.
/// </summary>
public class PayrollRunReviewRequest
{
	public string Month { get; set; } = string.Empty;
	public Guid? AcademicYearId { get; set; }
	public List<Guid> EmployeeIds { get; set; } = new();
}

public class PayrollAdjustmentRequestValidator : AbstractValidator<PayrollAdjustmentRequest>
{
	public PayrollAdjustmentRequestValidator()
	{
		RuleFor(x => x.Month).NotEmpty().MaximumLength(30);
		RuleFor(x => x.EmployeeId).NotEmpty();
		RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
		RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
		RuleFor(x => x.Description).MaximumLength(1000);
	}
}

public class PayrollRunReviewRequestValidator : AbstractValidator<PayrollRunReviewRequest>
{
	public PayrollRunReviewRequestValidator()
	{
		RuleFor(x => x.Month).NotEmpty().MaximumLength(30);
		RuleFor(x => x.EmployeeIds).NotNull().Must(x => x.Count > 0);
		RuleForEach(x => x.EmployeeIds).NotEmpty();
	}
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
	/// Optional academic year scope. When supplied, the payroll month must fall inside this academic year.
	/// </summary>
	public Guid? AcademicYearId { get; set; }

	/// <summary>
	/// Page index (0-based)
	/// </summary>
	public int Page { get; set; }

	/// <summary>
	/// Page size
	/// </summary>
	public int PageSize { get; set; } = 50;

	/// <summary>
	/// Forces recomputation and snapshot refresh for the month.
	/// </summary>
	public bool ForceRefresh { get; set; }
}
