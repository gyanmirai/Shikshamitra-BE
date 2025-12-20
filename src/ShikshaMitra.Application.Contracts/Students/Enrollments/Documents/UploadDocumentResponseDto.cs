using System;

namespace ShikshaMitra.Students.StudentEnrollments.Documents;

/// <summary>
/// Response DTO for document upload operation
/// </summary>
public class UploadDocumentResponseDto
{
	/// <summary>
	/// ID of the uploaded document
	/// </summary>
	public Guid DocumentId { get; set; }

	/// <summary>
	/// Success message
	/// </summary>
	public string Message { get; set; } = string.Empty;

	/// <summary>
	/// Indicates whether an existing document was replaced
	/// </summary>
	public bool WasReplaced { get; set; }
}
