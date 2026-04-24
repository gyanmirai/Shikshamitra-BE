using Microsoft.AspNetCore.Http;

namespace ShikshaMitra.HttpApi.Models;

public class AttendanceImportUploadRequest
{
	public IFormFile? File { get; set; }
}
