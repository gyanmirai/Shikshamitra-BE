using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

/// <summary>
/// Multipart request used by the single-chapter form. Payload contains the
/// chapter JSON and Files contains newly selected attachments.
/// </summary>
public class ChapterMultipartRequest
{
	[Required]
	public string Payload { get; set; } = null!;

	[DisableValidation]
	public List<IFormFile> Files { get; set; } = [];
}
