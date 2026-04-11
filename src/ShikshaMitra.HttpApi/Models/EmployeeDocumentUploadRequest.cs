using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class EmployeeDocumentUploadRequest
{
	[Required]
	public Guid DocumentNameId { get; set; }

	[Required]
	[DisableValidation]
	public IFormFile File { get; set; } = null!;

	public string? FileName { get; set; }
}
