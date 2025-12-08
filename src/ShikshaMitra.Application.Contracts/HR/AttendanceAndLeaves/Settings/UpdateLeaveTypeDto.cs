using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// DTO for updating a leave type
/// </summary>
public class UpdateLeaveTypeDto
{
	/// <summary>
	/// Type/Name of the leave
	/// </summary>
	[Required]
	[StringLength(LeaveTypeConsts.MaxTypeLength, MinimumLength = 1)]
	public string Type { get; set; } = string.Empty;
	
	/// <summary>
	/// Color code in HEX format (#RRGGBB)
	/// </summary>
	[Required]
	[StringLength(LeaveTypeConsts.MaxColorLength, MinimumLength = 7)]
	[RegularExpression(@"^#[A-Fa-f0-9]{6}$", ErrorMessage = "Color must be in HEX format (#RRGGBB)")]
	public string Color { get; set; } = string.Empty;
}
