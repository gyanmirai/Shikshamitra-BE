namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for creating a leave type
/// </summary>
public class CreateLeaveTypeDto
{
	/// <summary>
	/// Type/Name of the leave (must be unique)
	/// </summary>
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// Color code in HEX format (#RRGGBB)
	/// </summary>
	public string Color { get; set; } = string.Empty;
}
