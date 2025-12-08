namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Type of deduction component calculation
/// </summary>
public enum DeductionComponentType
{
	/// <summary>
	/// Fixed amount component (e.g., PF = 1800)
	/// </summary>
	Fixed = 0,
	
	/// <summary>
	/// Variable/percentage-based component (e.g., Tax = 10% of Gross)
	/// </summary>
	Variable = 1
}
