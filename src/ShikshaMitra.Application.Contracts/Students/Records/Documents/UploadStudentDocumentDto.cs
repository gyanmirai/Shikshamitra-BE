using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Records.Documents;

/// <summary>
/// DTO for uploading a document (used with multipart/form-data)
/// </summary>
public class UploadStudentDocumentDto
{
	/// <summary>
	/// The document type ID from settings
	/// </summary>
	[Required]
	public Guid DocumentTypeId { get; set; }
}
