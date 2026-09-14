using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

/// <summary>
/// Response for payroll preview (GET /payroll/run)
/// </summary>
public class RunPayrollResponse
{
	public Guid? PreviewRunId { get; set; }
	public bool IsCurrent { get; set; }
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
