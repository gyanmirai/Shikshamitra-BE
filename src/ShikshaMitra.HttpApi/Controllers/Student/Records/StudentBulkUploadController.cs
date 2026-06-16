using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using System.IO;
using System.Threading;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students")]
public class StudentBulkUploadController(
	IStudentBulkUploadAppService studentBulkUploadAppService
) : AbpController
{
	private const long MaxUploadBytes = 2 * 1024 * 1024;

	[HttpPost("bulk-upload")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentManagement.Add)]
	[ProducesResponseType(typeof(Result<StudentBulkUploadResultDto>), StatusCodes.Status200OK)]
	public async Task<Result<StudentBulkUploadResultDto>> Upload([FromForm] StudentBulkUploadRequest request)
	{
		var file = request.Files ?? request.File;
		if (file is null || file.Length == 0)
		{
			throw new BadHttpRequestException("No bulk student upload file was provided.");
		}

		if (file.Length > MaxUploadBytes)
		{
			throw new BadHttpRequestException("Bulk student upload file exceeds the allowed size.");
		}

		var content = await ReadBoundedAsync(file, MaxUploadBytes, HttpContext.RequestAborted);
		var result = await studentBulkUploadAppService.UploadAsync(new StudentBulkUploadFileDto
		{
			FileName = file.FileName,
			ContentType = file.ContentType,
			Content = content
		});

		return Result.Success(result);
	}

	private static async Task<byte[]> ReadBoundedAsync(
		IFormFile file,
		long maxBytes,
		CancellationToken cancellationToken)
	{
		await using var stream = file.OpenReadStream();
		using var memory = new MemoryStream();
		var buffer = new byte[81920];
		long totalRead = 0;
		int read;

		while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
		{
			totalRead += read;
			if (totalRead > maxBytes)
			{
				throw new BadHttpRequestException("Bulk student upload file exceeds the allowed size.");
			}

			await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		return memory.ToArray();
	}
}

public class StudentBulkUploadRequest
{
	public IFormFile? Files { get; set; }
	public IFormFile? File { get; set; }
}
