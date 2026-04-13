using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Employees.Payroll;
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

	/// <summary>
	/// Employee identifier
	/// </summary>
	public Guid EmployeeId { get; private set; }

	/// <summary>
	/// Month and year in YYYY-MM format
	/// </summary>
	public string MonthYear { get; private set; } = string.Empty;

	/// <summary>
	/// Parsed year component for efficient querying
	/// </summary>
	public int Year { get; private set; }

	/// <summary>
	/// Parsed month component for efficient querying (1-12)
	/// </summary>
	public int Month { get; private set; }

	/// <summary>
	/// Total earnings amount
	/// </summary>
	public decimal TotalEarning { get; private set; }

	/// <summary>
	/// Total deductions amount
	/// </summary>
	public decimal TotalDeduction { get; private set; }

	/// <summary>
	/// Net salary (TotalEarning - TotalDeduction)
	/// </summary>
	public decimal NetSalary { get; private set; }

	/// <summary>
	/// Salary slip status
	/// </summary>
	public SalarySlipStatus Status { get; private set; }

	/// <summary>
	/// Processing date when the slip was generated
	/// </summary>
	public DateTime ProcessedDate { get; private set; }

	/// <summary>
	/// Optional remarks or notes
	/// </summary>
	public string? Remarks { get; private set; }

	/// <summary>
	/// Salary slip line items for earnings
	/// </summary>
	public List<SalarySlipLineItem> Earnings { get; private set; } = new();

	/// <summary>
	/// Salary slip line items for deductions
	/// </summary>
	public List<SalarySlipLineItem> Deductions { get; private set; } = new();

	/// <summary>
	/// Payroll day basis used for LOP proration (denominator days).
	/// </summary>
	public int? PayrollBasisDays { get; private set; }

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

	private SalarySlip() { } // For ORM

	internal SalarySlip(
		Guid id,
		Guid? tenantId,
		Guid employeeId,
		string monthYear,
		List<SalarySlipLineItem> earnings,
		List<SalarySlipLineItem> deductions,
		string? remarks = null) : base(id)
	{
		TenantId = tenantId;
		EmployeeId = employeeId;
		SetMonthYear(monthYear);
		Earnings = earnings ?? new List<SalarySlipLineItem>();
		Deductions = deductions ?? new List<SalarySlipLineItem>();
		Remarks = remarks;
		Status = SalarySlipStatus.Draft;
		ProcessedDate = DateTime.UtcNow;

		RecalculateTotals();
	}

	/// <summary>
	/// Recalculates total earnings, deductions, and net salary
	/// </summary>
	private void RecalculateTotals()
	{
		TotalEarning = Earnings.Sum(e => e.Amount);
		TotalDeduction = Deductions.Sum(d => d.Amount);
		NetSalary = TotalEarning - TotalDeduction;
	}

	/// <summary>
	/// Sets and parses the month-year value
	/// </summary>
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

	/// <summary>
	/// Marks the salary slip as finalized (immutable)
	/// </summary>
	public void Finalize()
	{
		if (Status == SalarySlipStatus.Finalized)
			throw new InvalidOperationException("Salary slip is already finalized");

		Status = SalarySlipStatus.Finalized;
	}

	/// <summary>
	/// Updates remarks (only allowed in draft status)
	/// </summary>
	public void UpdateRemarks(string? remarks)
	{
		if (Status == SalarySlipStatus.Finalized)
			throw new InvalidOperationException("Cannot update finalized salary slip");

		Remarks = remarks;
	}

	/// <summary>
	/// Updates earnings and deductions line items (only allowed in draft status)
	/// </summary>
	internal void UpdateLineItems(
		List<SalarySlipLineItem> earnings,
		List<SalarySlipLineItem> deductions)
	{
		if (Status == SalarySlipStatus.Finalized)
			throw new InvalidOperationException("Cannot update finalized salary slip");

		Earnings = earnings ?? new List<SalarySlipLineItem>();
		Deductions = deductions ?? new List<SalarySlipLineItem>();
		ProcessedDate = DateTime.UtcNow;
		RecalculateTotals();
	}

	/// <summary>
	/// Stores payroll day basis snapshot for audit (draft only).
	/// </summary>
	internal void SetPayrollComputationSnapshot(
		int basisDays,
		decimal payableDays,
		decimal factor,
		DayCalculationType dayMode)
	{
		if (Status == SalarySlipStatus.Finalized)
			throw new InvalidOperationException("Cannot update finalized salary slip");

		PayrollBasisDays = basisDays;
		PayableDays = payableDays;
		ProrationFactor = factor;
		PayrollDayCalculationMode = dayMode;
	}
}

/// <summary>
/// Represents a single line item in a salary slip (earning or deduction)
/// Enhanced to include component metadata for OpenAPI compliance
/// </summary>
public class SalarySlipLineItem
{
	/// <summary>
	/// Component name (e.g., "Basic", "HRA", "PF")
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Amount for this component
	/// </summary>
	public decimal Amount { get; set; }

	/// <summary>
	/// Optional description or calculation formula
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Whether this is a fixed component for all employees
	/// </summary>
	public bool IsFixed { get; set; }

	/// <summary>
	/// Whether this is a temporary or one-time component
	/// </summary>
	public bool IsTemp { get; set; }

	/// <summary>
	/// Calculation strategy (Fixed or Percentage)
	/// </summary>
	public SalaryComponentType ComponentType { get; set; }

	/// <summary>
	/// What this component depends on (None, Basic, or Gross)
	/// </summary>
	public SalaryComponentDependency DependOn { get; set; }

	public SalarySlipLineItem() { }

	public SalarySlipLineItem(
		string name,
		decimal amount,
		string? description = null,
		bool isFixed = true,
		bool isTemp = false,
		SalaryComponentType componentType = SalaryComponentType.Fixed,
		SalaryComponentDependency dependOn = SalaryComponentDependency.None)
	{
		Name = name;
		Amount = amount;
		Description = description;
		IsFixed = isFixed;
		IsTemp = isTemp;
		ComponentType = componentType;
		DependOn = dependOn;
	}
}
