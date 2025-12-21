namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>
/// Status of salary slip
/// </summary>
public enum SalarySlipStatus
{
	/// <summary>
	/// Draft status - can be modified
	/// </summary>
	Draft = 0,
	
	/// <summary>
	/// Finalized - immutable
	/// </summary>
	Finalized = 1,
	
	/// <summary>
	/// Paid - salary has been disbursed
	/// </summary>
	Paid = 2
}
