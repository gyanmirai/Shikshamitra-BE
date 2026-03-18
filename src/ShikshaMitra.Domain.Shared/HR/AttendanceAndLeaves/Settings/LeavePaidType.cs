namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Type of leave payment
/// </summary>
public enum LeavePaidType
{
	/// <summary>
	/// Paid leave - salary is paid during leave
	/// </summary>
	Paid = 0,

	/// <summary>
	/// Unpaid leave - salary is deducted during leave
	/// </summary>
	Unpaid = 1
}
