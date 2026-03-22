namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Type of deduction component calculation.
/// Intentionally separate from <see cref="EarningComponentType"/> — deductions and earnings
/// carry different business semantics (e.g., statutory deductions, employer contributions)
/// and may diverge in future (e.g., a deduction-specific "Slab-based" type).
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
