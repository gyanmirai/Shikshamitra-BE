namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Defines what a percentage-based salary component depends on for calculation.
/// </summary>
public enum SalaryComponentDependency
{
	/// <summary>
	/// No dependency - used for fixed amount components.
	/// </summary>
	None = 0,
	
	/// <summary>
	/// Depends on Basic Salary.
	/// </summary>
	Basic = 1,
	
	/// <summary>
	/// Depends on Gross Salary.
	/// </summary>
	Gross = 2
}

