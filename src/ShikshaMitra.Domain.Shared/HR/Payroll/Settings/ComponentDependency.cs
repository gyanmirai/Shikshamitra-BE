namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Defines what a component depends on for calculation
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
