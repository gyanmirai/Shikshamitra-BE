using System.ComponentModel.DataAnnotations;
using ShikshaMitra.HR.Employees.Payroll;

namespace ShikshaMitra.HR.Payroll.SalaryStructure;

/// <summary>
/// DTO representing a salary component (earning or deduction) as per OpenAPI spec.
/// </summary>
public class SalaryComponentDto
{
	/// <summary>
	/// Component name (e.g., Basic, PF, Tax).
	/// </summary>
	[Required]
	[MinLength(2)]
	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;
	
	/// <summary>
	/// Optional explanation of the component.
	/// </summary>
	[MaxLength(255)]
	public string? Description { get; set; }
	
	/// <summary>
	/// Final calculated amount.
	/// </summary>
	[Range(0, double.MaxValue)]
	public decimal Amount { get; set; }
	
	/// <summary>
	/// Fixed for all employees or employee-specific.
	/// </summary>
	[Required]
	public bool IsFixed { get; set; }
	
	/// <summary>
	/// Temporary or one-time component.
	/// </summary>
	[Required]
	public bool IsTemp { get; set; }
	
	/// <summary>
	/// Calculation strategy (FIXED or PERCENTAGE).
	/// </summary>
	[Required]
	public SalaryComponentType ComponentType { get; set; }
	
	/// <summary>
	/// Required only when componentType is PERCENTAGE.
	/// Indicates what the percentage is based on (BASIC or GROSS).
	/// </summary>
	public SalaryComponentDependency? DependOn { get; set; }
}

