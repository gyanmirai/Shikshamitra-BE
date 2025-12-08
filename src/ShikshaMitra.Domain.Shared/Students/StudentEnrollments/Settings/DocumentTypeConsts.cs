namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Constants for DocumentType entity
/// </summary>
public static class DocumentTypeConsts
{
	/// <summary>
	/// Maximum length of document type name
	/// </summary>
	public const int MaxNameLength = 100;

	/// <summary>
	/// Maximum size in MB (will be validated in DTO)
	/// </summary>
	public const double MaxDocumentSizeMB = 100.0;

	/// <summary>
	/// Minimum size in MB
	/// </summary>
	public const double MinDocumentSizeMB = 0.01;
}
