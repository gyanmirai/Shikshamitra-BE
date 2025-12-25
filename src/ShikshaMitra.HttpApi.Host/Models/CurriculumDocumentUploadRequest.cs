using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Models;

public class CurriculumDocumentUploadRequest
{
	public required string FileName { get; set; }

	/// <summary>
	/// The profile picture file to upload
	/// </summary>
	public required IFormFile File { get; set; }
}
