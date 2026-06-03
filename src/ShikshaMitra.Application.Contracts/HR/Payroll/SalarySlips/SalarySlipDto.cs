using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// DTO for salary slip details with earnings and deductions breakdown
/// Optimized for frontend consumption
/// </summary>
public class SalarySlipDto
{
	/// <summary>
	/// Salary slip identifier
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Employee identifier
	/// </summary>
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// Employee name (for display purposes)
	/// </summary>
	public string? EmployeeName { get; set; }

	/// <summary>
	/// Employee code
	/// </summary>
	public string? EmployeeCode { get; set; }

	public string? Department { get; set; }

	public string? Designation { get; set; }

	public DateOnly? DateOfJoining { get; set; }

	public string? SlipNumber { get; set; }

	/// <summary>
	/// Month and year in YYYY-MM format
	/// </summary>
	public string MonthYear { get; set; } = string.Empty;

	/// <summary>
	/// Total earning amount
	/// </summary>
	public decimal TotalEarning { get; set; }

	/// <summary>
	/// Total deduction amount
	/// </summary>
	public decimal TotalDeduction { get; set; }

	/// <summary>
	/// Net salary (TotalEarning - TotalDeduction)
	/// </summary>
	public decimal NetSalary { get; set; }

	/// <summary>
	/// List of earnings breakdown
	/// </summary>
	public List<SalarySlipLineItemDto> Earnings { get; set; } = new();

	/// <summary>
	/// List of deductions breakdown
	/// </summary>
	public List<SalarySlipLineItemDto> Deductions { get; set; } = new();

	/// <summary>
	/// Salary slip status
	/// </summary>
	public SalarySlipStatus Status { get; set; }

	/// <summary>
	/// Optional remarks
	/// </summary>
	public string? Remarks { get; set; }

	/// <summary>
	/// Processing date
	/// </summary>
	public DateTime ProcessedDate { get; set; }

	/// <summary>
	/// Payroll basis days (denominator) when slip was computed.
	/// </summary>
	public decimal? PayrollBasisDays { get; set; }

	/// <summary>
	/// Payable days from attendance at computation time.
	/// </summary>
	public decimal? PayableDays { get; set; }

	public decimal? PresentDays { get; set; }

	public decimal? HalfWorkingDays { get; set; }

	public decimal? LopDays { get; set; }

	/// <summary>
	/// LOP proration factor applied (capped at 1).
	/// </summary>
	public decimal? ProrationFactor { get; set; }

	/// <summary>
	/// Day calculation mode: working-days, total-month-days, or manual-days.
	/// </summary>
	public string? PayrollDayCalculationMode { get; set; }

	public string? BankName { get; set; }

	public string? MaskedAccountNumber { get; set; }

	public string? IfscCode { get; set; }

	public string? UanNumber { get; set; }

	public string? EpfNumber { get; set; }

	public string? NetPayInWords { get; set; }
}
