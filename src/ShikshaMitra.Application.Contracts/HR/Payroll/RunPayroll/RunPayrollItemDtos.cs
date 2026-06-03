using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

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
	/// Payable days from attendance: present +0.5 x half days.
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
