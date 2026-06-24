using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class TransportVehicleDocumentUploadRequest
{
	[Required]
	public Guid RequiredDocumentId { get; set; }

	[Required]
	[DisableValidation]
	public IFormFile File { get; set; } = null!;

	public string? FileName { get; set; }
}
