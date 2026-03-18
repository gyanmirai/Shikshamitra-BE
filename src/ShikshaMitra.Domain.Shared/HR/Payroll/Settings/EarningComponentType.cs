namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Type of earning component calculation
/// </summary>
public enum EarningComponentType
{
	/// <summary>
	/// Fixed amount component (e.g., Basic Salary = 30000)
	/// </summary>
	Fixed = 0,

	/// <summary>
	/// Variable/percentage-based component (e.g., HRA = 40% of Basic)
	/// </summary>
	Variable = 1
}
