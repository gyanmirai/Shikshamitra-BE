using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Represents a monthly salary slip for an employee with earnings and deductions.
/// Immutable once generated to maintain payroll history integrity.
/// </summary>
public class SalarySlip : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid EmployeeId { get; private set; }

	public string MonthYear { get; private set; } = string.Empty;

	public int Year { get; private set; }

	public int Month { get; private set; }

	public decimal TotalEarning { get; private set; }

	public decimal TotalDeduction { get; private set; }

	public decimal NetSalary { get; private set; }

	public SalarySlipStatus Status { get; private set; }

	public DateTime ProcessedDate { get; private set; }

	public string? Remarks { get; private set; }

	public List<SalarySlipLineItem> Earnings { get; private set; } = new();

	public List<SalarySlipLineItem> Deductions { get; private set; } = new();

	/// <summary>
	/// Payroll day basis used for LOP proration (denominator days).
	/// </summary>
	public decimal? PayrollBasisDays { get; private set; }

	/// <summary>
	/// Payable days from attendance (present + half-days as 0.5).
	/// </summary>
	public decimal? PayableDays { get; private set; }

	/// <summary>
	/// Applied LOP factor (capped at 1).
	/// </summary>
	public decimal? ProrationFactor { get; private set; }

	/// <summary>
	/// Day calculation mode active when the slip was computed.
	/// </summary>
	public DayCalculationType? PayrollDayCalculationMode { get; private set; }

	public Guid? PayrollPolicyId { get; private set; }
	public int? PayrollPolicyVersion { get; private set; }
	public Guid? AttendancePolicyId { get; private set; }
	public int? AttendancePolicyVersion { get; private set; }
	public Guid? WorkCalendarPolicyId { get; private set; }
	public int? WorkCalendarPolicyVersion { get; private set; }

	private SalarySlip() { } // For ORM

	internal SalarySlip(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		string monthYear,
		List<SalarySlipLineItem> earnings,
		List<SalarySlipLineItem> deductions,
		DateTime processedDate,
		string? remarks = null) : base(id)
	{
		TenantId = tenantId;
		EmployeeId = employeeId;
		SetMonthYear(monthYear);
		Earnings = earnings ?? new List<SalarySlipLineItem>();
		Deductions = deductions ?? new List<SalarySlipLineItem>();
		Remarks = remarks;
		Status = SalarySlipStatus.Draft;
		ProcessedDate = processedDate;

		RecalculateTotals();
	}

	private void RecalculateTotals()
	{
		TotalEarning = Earnings.Sum(e => e.Amount);
		TotalDeduction = Deductions.Sum(d => d.Amount);
		NetSalary = TotalEarning - TotalDeduction;
	}

	private void SetMonthYear(string monthYear)
	{
		if (string.IsNullOrWhiteSpace(monthYear))
			throw new ArgumentException("MonthYear cannot be empty", nameof(monthYear));

		if (!DateTime.TryParseExact(monthYear + "-01", "yyyy-MM-dd", null,
			System.Globalization.DateTimeStyles.None, out var date))
		{
			throw new ArgumentException("MonthYear must be in YYYY-MM format", nameof(monthYear));
		}

		MonthYear = monthYear;
		Year = date.Year;
		Month = date.Month;
	}

	public void FinalizeSlip()
	{
		if (Status == SalarySlipStatus.Finalized)
			throw new InvalidOperationException("Salary slip is already finalized");

		Status = SalarySlipStatus.Finalized;
	}

	public void UpdateRemarks(string? remarks)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		Remarks = remarks;
	}

	internal void UpdateLineItems(
		List<SalarySlipLineItem> earnings,
		List<SalarySlipLineItem> deductions,
		DateTime processedDate)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		Earnings = earnings ?? new List<SalarySlipLineItem>();
		Deductions = deductions ?? new List<SalarySlipLineItem>();
		ProcessedDate = processedDate;
		RecalculateTotals();
	}

	/// <summary>
	/// Stores payroll day basis snapshot for audit (draft only).
	/// </summary>
	internal void SetPayrollComputationSnapshot(
		decimal basisDays,
		decimal payableDays,
		decimal factor,
		DayCalculationType dayMode)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		PayrollBasisDays = basisDays;
		PayableDays = payableDays;
		ProrationFactor = factor;
		PayrollDayCalculationMode = dayMode;
	}

	internal void SetPolicySnapshot(
		Guid? payrollPolicyId,
		int? payrollPolicyVersion,
		Guid? attendancePolicyId,
		int? attendancePolicyVersion,
		Guid? workCalendarPolicyId,
		int? workCalendarPolicyVersion)
	{
		if (IsImmutable())
			throw new InvalidOperationException("Cannot update finalized or paid salary slip");

		PayrollPolicyId = payrollPolicyId;
		PayrollPolicyVersion = payrollPolicyVersion;
		AttendancePolicyId = attendancePolicyId;
		AttendancePolicyVersion = attendancePolicyVersion;
		WorkCalendarPolicyId = workCalendarPolicyId;
		WorkCalendarPolicyVersion = workCalendarPolicyVersion;
	}

	private bool IsImmutable() => Status is SalarySlipStatus.Finalized or SalarySlipStatus.Paid;
}
