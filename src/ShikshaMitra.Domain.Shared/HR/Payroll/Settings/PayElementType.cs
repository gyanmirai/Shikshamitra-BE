namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Defines the type of pay element (credit or debit).
/// </summary>
public enum PayElementType
{
	/// <summary>
	/// Credit entry — added to the employee's pay.
	/// </summary>
	Credit = 0,

	/// <summary>
	/// Debit entry — deducted from the employee's pay.
	/// </summary>
	Debit = 1
}