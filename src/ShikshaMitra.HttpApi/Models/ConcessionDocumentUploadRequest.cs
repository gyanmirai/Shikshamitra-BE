using Microsoft.AspNetCore.Http;
using Volo.Abp.Validation;

namespace ShikshaMitra.Models;

public class ConcessionDocumentUploadRequest
{
	[DisableValidation]
	public required IFormFile File { get; set; }
}
