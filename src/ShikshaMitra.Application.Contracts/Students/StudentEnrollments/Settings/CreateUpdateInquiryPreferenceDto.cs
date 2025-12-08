using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// DTO for creating or updating an inquiry preference
/// </summary>
public class CreateUpdateInquiryPreferenceDto
{
	/// <summary>
	/// Name of the inquiry preference (e.g., "Online", "Offline", "Facebook", "Walk-in")
	/// </summary>
	[Required(ErrorMessage = "Preference name is required")]
	[StringLength(
		InquiryPreferenceConsts.MaxPreferenceNameLength,
		MinimumLength = 2,
		ErrorMessage = "Preference name must be between 2 and {1} characters")]
	public string PreferenceName { get; set; } = string.Empty;
}
