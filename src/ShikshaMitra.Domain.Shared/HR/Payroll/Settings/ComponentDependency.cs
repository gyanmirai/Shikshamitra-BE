namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Defines what a payroll-settings component depends on for calculation.
/// This is the master-level enum used in earning/deduction component templates.
/// <see cref="ShikshaMitra.HR.Employees.Payroll.SalaryComponentDependency"/> is the
/// employee-level variant that omits <see cref="CustomComponent"/>.
/// </summary>
public enum ComponentDependency
{
	/// <summary>
	/// No dependency - fixed amount
	/// </summary>
	None = 0,

	/// <summary>
	/// Depends on Basic Salary
	/// </summary>
	Basic = 1,

	/// <summary>
	/// Depends on Gross Salary
	/// </summary>
	Gross = 2,

	/// <summary>
	/// Depends on Any Custom Salary Component
	/// </summary>
	CustomComponent = 3
}
