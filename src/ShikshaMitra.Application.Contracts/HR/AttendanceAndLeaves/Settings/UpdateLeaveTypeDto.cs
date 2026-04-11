namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for updating a leave type
/// </summary>
public class UpdateLeaveTypeDto
{
	/// <summary>
	/// Type/Name of the leave
	/// </summary>
	public string Type { get; set; } = string.Empty;

	/// <summary>
	/// Color code in HEX format (#RRGGBB)
	/// </summary>
	public string Color { get; set; } = string.Empty;

	/// <summary>
	/// Optional description of the leave type
	/// </summary>
	public string? Description { get; set; }
}
