using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Students.Records.Documents;

/// <summary>
/// Internal DTO after controller processes IFormFile
/// </summary>
public class ProcessedStudentDocumentDto
{
	[Required]
	public Guid DocumentTypeId { get; set; }

	[Required]
	public string FileName { get; set; } = string.Empty;

	[Required]
	public string ContentType { get; set; } = string.Empty;

	[Required]
	[Range(1, long.MaxValue)]
	public long Size { get; set; }

	[Required]
	public byte[] Content { get; set; } = Array.Empty<byte>();
}
