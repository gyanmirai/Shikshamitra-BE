namespace ShikshaMitra.HR.Employees.Payroll;

/// <summary>
/// Defines what a percentage-based salary component depends on for calculation.
/// This is the employee-level variant of
/// <see cref="ShikshaMitra.HR.Payroll.Settings.ComponentDependency"/> and intentionally
/// omits <c>CustomComponent</c>, which is only relevant at the master-settings level.
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

