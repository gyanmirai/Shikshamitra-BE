using ShikshaMitra.HR.Employees.Payroll;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Represents a single line item in a salary slip (earning or deduction).
/// </summary>
public class SalarySlipLineItem
{
	/// <summary>
	/// Component name (e.g., "Basic", "HRA", "PF").
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Amount for this component.
	/// </summary>
	public decimal Amount { get; set; }

	/// <summary>
	/// Optional description or calculation formula.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// Whether this is a fixed component for all employees.
	/// </summary>
	public bool IsFixed { get; set; }

	/// <summary>
	/// Whether this is a temporary or one-time component.
	/// </summary>
	public bool IsTemp { get; set; }

	/// <summary>
	/// Calculation strategy (Fixed or Percentage).
	/// </summary>
	public SalaryComponentType ComponentType { get; set; }

	/// <summary>
	/// What this component depends on (None, Basic, or Gross).
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
