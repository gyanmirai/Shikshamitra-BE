using System;

namespace ShikshaMitra.Students.StudentEnrollments.Documents;

/// <summary>
/// DTO for downloading document with content
/// </summary>
public class StudentDocumentDownloadDto
{
	/// <summary>
	/// Original filename
	/// </summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>
	/// MIME type
	/// </summary>
	public string ContentType { get; set; } = string.Empty;

	/// <summary>
	/// File content
	/// </summary>
	public byte[] Content { get; set; } = Array.Empty<byte>();
}
