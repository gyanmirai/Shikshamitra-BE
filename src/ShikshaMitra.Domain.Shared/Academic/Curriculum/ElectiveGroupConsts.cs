namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Validation constants for Elective Group entity
/// </summary>
public static class ElectiveGroupConsts
{
	/// <summary>
	/// Minimum length for elective group name
	/// </summary>
	public const int MinNameLength = 2;

	/// <summary>
	/// Maximum length for elective group name
	/// </summary>
	public const int MaxNameLength = 100;

	/// <summary>
	/// Minimum number of subjects required in an elective group
	/// At least 2 subjects are needed to provide a choice for students
	/// </summary>
	public const int MinSubjectsRequired = 2;

	// Note: Color validation uses SubjectConsts.ColorRegexPattern
	// Color format: #RGB or #RRGGBB (hex color codes)
}
