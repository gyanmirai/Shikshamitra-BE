using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Attendances.Settings;

/// <summary>
/// DTO for creating and updating student leave types
/// </summary>
public class CreateUpdateStudentLeaveTypeDto
{
	/// <summary>
	/// Name of the leave type (e.g., "Sick", "Casual Leave")
	/// </summary>
	[Required]
	[StringLength(100, MinimumLength = 2)]
	public string LeaveTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Color code in hex format (e.g., "#FF5733")
	/// </summary>
	[Required]
	[StringLength(7)]
	[RegularExpression(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$",
		ErrorMessage = "Color must be a valid hex color code (e.g., #FF5733)")]
	public string Color { get; set; } = string.Empty;

	/// <summary>
	/// Indicates if this leave type is active
	/// </summary>
	public bool IsActive { get; set; } = true;
}
