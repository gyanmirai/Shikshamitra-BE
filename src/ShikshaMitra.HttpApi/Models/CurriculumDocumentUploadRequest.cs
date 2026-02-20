using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class CurriculumDocumentUploadRequest
{
	/// <summary>
	/// The file to upload
	/// </summary>
	[DisableValidation]
	public required IFormFile File { get; set; }
}
