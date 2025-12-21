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
	public string Status { get; set; } = string.Empty;

	/// <summary>
	/// Optional remarks
	/// </summary>
	public string? Remarks { get; set; }

	/// <summary>
	/// Processing date
	/// </summary>
	public DateTime ProcessedDate { get; set; }
}

/// <summary>
/// DTO for salary slip line item (earning or deduction)
/// </summary>
public class SalarySlipLineItemDto
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
	/// Optional description or calculation details
	/// </summary>
	public string? Description { get; set; }
}

/// <summary>
/// Input DTO for fetching salary slip
/// </summary>
public class GetSalarySlipInput
{
	/// <summary>
	/// Employee identifier
	/// </summary>
	public Guid EmployeeId { get; set; }

	/// <summary>
	/// Month and year in YYYY-MM format (e.g., "2025-01")
	/// </summary>
	public string MonthYear { get; set; } = string.Empty;
}
