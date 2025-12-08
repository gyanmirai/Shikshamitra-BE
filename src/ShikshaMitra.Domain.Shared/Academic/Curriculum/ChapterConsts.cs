namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Validation constants for chapter management
/// </summary>
public static class ChapterConsts
{
	/// <summary>
	/// Minimum length for chapter name
	/// </summary>
	public const int MinChapterNameLength = 3;

	/// <summary>
	/// Maximum length for chapter name
	/// </summary>
	public const int MaxChapterNameLength = 200;

	/// <summary>
	/// Maximum length for chapter description (HTML content)
	/// </summary>
	public const int MaxDescriptionLength = 5000;

	/// <summary>
	/// Maximum length for file name
	/// </summary>
	public const int MaxFileNameLength = 255;

	/// <summary>
	/// Maximum length for file URL
	/// </summary>
	public const int MaxFileUrlLength = 2048;

	/// <summary>
	/// Maximum file size in bytes (5 MB)
	/// </summary>
	public const long MaxFileSizeBytes = 5 * 1024 * 1024;

	/// <summary>
	/// Allowed file MIME types for chapter documents
	/// </summary>
	public static readonly string[] AllowedFileTypes = new[]
	{
		"application/pdf", // PDF files
		"application/msword", // DOC files
		"application/vnd.openxmlformats-officedocument.wordprocessingml.document", // DOCX files
		"application/vnd.ms-powerpoint", // PPT files
		"application/vnd.openxmlformats-officedocument.presentationml.presentation", // PPTX files
		"text/plain", // TXT files
		"application/vnd.ms-excel", // XLS files
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" // XLSX files
	};
}
