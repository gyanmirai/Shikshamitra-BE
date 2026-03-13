namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Defines the calculation type for salary components.
/// </summary>
public enum SalaryComponentType
{
	/// <summary>
	/// Fixed amount component.
	/// </summary>
	Fixed = 0,

	/// <summary>
	/// Percentage-based component (depends on Basic or Gross).
	/// </summary>
	Percentage = 1
}

