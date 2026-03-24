namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Type of earning component calculation.
/// Intentionally separate from <see cref="DeductionComponentType"/> — earnings and deductions
/// carry different business semantics (e.g., tax treatment, statutory rules) and may diverge
/// in future (e.g., an earning-specific "Percentage-of-CTC" type).
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
