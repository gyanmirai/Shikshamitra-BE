using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// DTO for creating or updating a document type
/// </summary>
public class CreateUpdateDocumentTypeDto
{
	/// <summary>
	/// Name of the document type (e.g., "Birth Certificate", "PAN Card")
	/// </summary>
	[Required(ErrorMessage = "Document name is required")]
	[StringLength(
		DocumentTypeConsts.MaxNameLength,
		MinimumLength = 2,
		ErrorMessage = "Document name must be between 2 and {1} characters")]
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Maximum allowed file size in MB
	/// </summary>
	[Required(ErrorMessage = "Maximum size is required")]
	[Range(
		DocumentTypeConsts.MinDocumentSizeMB,
		DocumentTypeConsts.MaxDocumentSizeMB,
		ErrorMessage = "Size must be between {1} and {2} MB")]
	public double MaxSizeMB { get; set; }

	/// <summary>
	/// List of accepted file formats (multi-select)
	/// </summary>
	[Required(ErrorMessage = "At least one file format must be selected")]
	[MinLength(1, ErrorMessage = "At least one file format must be selected")]
	public List<AcceptedFileFormat> AcceptedFormats { get; set; } = new();

	/// <summary>
	/// Whether this document type is required for student enrollment
	/// </summary>
	public bool IsRequired { get; set; }

	/// <summary>
	/// Display order for sorting (default 0)
	/// </summary>
	[Range(0, int.MaxValue, ErrorMessage = "Display order must be non-negative")]
	public int DisplayOrder { get; set; }
}
