using System;

using ShikshaMitra.Students.Documents;

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

	public string? Sha256Hash { get; set; }

	public StudentDocumentStorageStatus StorageStatus { get; set; } = StudentDocumentStorageStatus.Stored;

	public bool IsSensitive { get; set; }
}
