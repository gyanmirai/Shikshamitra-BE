using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Models;

public class CurriculumDocumentUploadRequest
{
	/// <summary>
	/// The profile picture file to upload
	/// </summary>
	public required IFormFile File { get; set; }
}
