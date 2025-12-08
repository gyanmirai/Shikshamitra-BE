namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Constants for Subject entity validation and constraints
/// </summary>
public static class SubjectConsts
{
	/// <summary>
	/// Maximum length for Subject Name field
	/// </summary>
	public const int MaxNameLength = 100;

	/// <summary>
	/// Minimum length for Subject Name field
	/// </summary>
	public const int MinNameLength = 2;

	/// <summary>
	/// Regular expression pattern for validating hex color codes
	/// Supports both 3-digit (#RGB) and 6-digit (#RRGGBB) formats
	/// </summary>
	public const string ColorRegexPattern = @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$";

	/// <summary>
	/// Error message for invalid color format
	/// </summary>
	public const string ColorFormatErrorMessage =
		"Color must be a valid hex color code (e.g., #FF5733 or #F57)";
}
