using System;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// DTO representing a salary component (earning or deduction) as per OpenAPI spec.
/// </summary>
public class SalaryComponentDto
{
	/// <summary>
	/// Salary component row ID (employee-specific).
	/// </summary>
	public Guid? Id { get; set; }

	/// <summary>
	/// Master component ID (earning/deduction setting ID).
	/// </summary>
	public Guid? ComponentId { get; set; }

	/// <summary>
	/// Component name (e.g., Basic, PF, Tax).
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Final calculated amount.
	/// </summary>
	public decimal? Amount { get; set; }

	/// <summary>
	/// Fixed for all employees or employee-specific.
	/// </summary>
	public bool IsFixed { get; set; }

	/// <summary>
	/// Temporary or one-time component.
	/// </summary>
	public bool IsTemp { get; set; }

	/// <summary>
	/// Month from which this component is applicable.
	/// </summary>
	public DateTime? ApplicableMonth { get; set; }
}
