using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ShikshaMitra.Models;

/// <summary>
/// Request model for uploading student documents via multipart/form-data
/// </summary>
public class StudentDocumentUploadRequest
{
	/// <summary>
	/// The document type ID from settings
	/// </summa
	[Required]
	public Guid DocumentTypeId { get; set; }

	/// <summary>
	/// The document file to upload
	/// </summary>
	[Required]
	public IFormFile File { get; set; } = null!;
}
