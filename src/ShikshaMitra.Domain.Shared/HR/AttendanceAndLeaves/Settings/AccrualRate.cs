namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Frequency at which leave balances are accrued under a leave policy.
/// </summary>
public enum AccrualRate
{
	/// <summary>
	/// Leaves are credited once per year.
	/// </summary>
	Yearly = 0,

	/// <summary>
	/// Leaves are credited once per month.
	/// </summary>
	Monthly = 1,

	/// <summary>
	/// Leaves are credited once per quarter.
	/// </summary>
	Quarterly = 2,
}