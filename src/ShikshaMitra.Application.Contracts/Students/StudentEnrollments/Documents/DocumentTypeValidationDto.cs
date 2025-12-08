using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.DocumentTypes;

namespace ShikshaMitra.Students.StudentEnrollments.Documents;

/// <summary>
/// DTO for document type validation status
/// </summary>
public class DocumentTypeValidationDto
{
	/// <summary>
	/// Document type identifier
	/// </summary>
	public Guid DocumentTypeId { get; set; }

	/// <summary>
	/// Document type name
	/// </summary>
	public string DocumentTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Whether this document type is required for enrollment
	/// </summary>
	public bool IsRequired { get; set; }

	/// <summary>
	/// Whether this document has been uploaded
	/// </summary>
	public bool IsUploaded { get; set; }

	/// <summary>
	/// ID of the uploaded document (if uploaded)
	/// </summary>
	public Guid? UploadedDocumentId { get; set; }

	/// <summary>
	/// Maximum file size allowed in MB
	/// </summary>
	public double MaxSizeMB { get; set; }

	/// <summary>
	/// List of accepted file formats
	/// </summary>
	public List<AcceptedFileFormat> AcceptedFormats { get; set; } = new();

	/// <summary>
	/// Display order for sorting
	/// </summary>
	public int DisplayOrder { get; set; }
}
