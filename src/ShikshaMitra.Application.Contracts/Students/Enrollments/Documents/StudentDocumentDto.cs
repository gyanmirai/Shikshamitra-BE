using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Students.StudentEnrollments.Documents;

/// <summary>
/// DTO for student document information
/// </summary>
public class StudentDocumentDto : EntityDto<Guid>
{
	/// <summary>
	/// Student identifier
	/// </summary>
	public Guid StudentId { get; set; }

	/// <summary>
	/// Document type identifier
	/// </summary>
	public Guid DocumentTypeId { get; set; }

	/// <summary>
	/// Document type name
	/// </summary>
	public string DocumentTypeName { get; set; } = string.Empty;

	/// <summary>
	/// Original filename
	/// </summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>
	/// MIME type
	/// </summary>
	public string ContentType { get; set; } = string.Empty;

	/// <summary>
	/// File size in bytes
	/// </summary>
	public long Size { get; set; }

	/// <summary>
	/// File size in MB (formatted)
	/// </summary>
	public double SizeMB => Math.Round(Size / (1024.0 * 1024.0), 2);

	/// <summary>
	/// When the document was uploaded
	/// </summary>
	public DateTime CreationTime { get; set; }

	/// <summary>
	/// Who uploaded the document
	/// </summary>
	public Guid? CreatorId { get; set; }

	public bool IsUploaded { get; set; } = false;
}
