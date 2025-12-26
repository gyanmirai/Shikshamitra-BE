using Microsoft.AspNetCore.Http;

namespace ShikshaMitra.HttpApi.Models;

public class EmployeeLeaveCreateUploadRequest
{
	public IFormFile? File { get; set; }
}
