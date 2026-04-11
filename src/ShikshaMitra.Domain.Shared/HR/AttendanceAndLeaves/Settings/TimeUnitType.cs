namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Unit of time used in attendance rules (e.g., late-arrival thresholds).
/// </summary>
public enum TimeUnitType
{
	/// <summary>
	/// Time measured in minutes.
	/// </summary>
	Minutes = 0,

	/// <summary>
	/// Time measured in hours.
	/// </summary>
	Hours = 1
}