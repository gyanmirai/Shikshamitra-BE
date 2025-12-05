namespace ShikshaMitra.Students.Attendance;

/// <summary>
/// Attendance status for a student on a particular day
/// </summary>
public enum AttendanceStatus
{
	/// <summary>
	/// Attendance not yet marked (default state)
	/// </summary>
	Pending = 0,

	/// <summary>
	/// Student was present
	/// </summary>
	Present = 1,

	/// <summary>
	/// Student was absent
	/// </summary>
	Absent = 2
}
