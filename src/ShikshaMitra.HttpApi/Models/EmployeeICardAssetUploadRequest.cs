using Microsoft.AspNetCore.Http;
using Volo.Abp.Validation;

namespace ShikshaMitra.HttpApi.Models;

public class EmployeeICardAssetUploadRequest
{
	[DisableValidation]
	public IFormFile? File { get; set; }
}
