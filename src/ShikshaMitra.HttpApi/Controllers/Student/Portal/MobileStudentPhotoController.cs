using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Portal;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.View)]
[Route("api/mobile/v1/students/{studentId:guid}/profile/photo")]
public class MobileStudentPhotoController(IMobileStudentPhotoAppService service) : AbpController
{
	private const int MaxBytes = 3 * 1024 * 1024;
	[HttpGet]
	public async Task<IActionResult> GetAsync(Guid studentId)
	{
		var photo = await service.GetPhotoAsync(studentId);
		return photo == null ? NotFound() : Download(photo);
	}

	[HttpPost("drafts")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add)]
	[RequestSizeLimit(MaxBytes + 64 * 1024)]
	[RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + 64 * 1024)]
	public async Task<Result<StudentRecordPhotoDraftDto>> UploadAsync(Guid studentId, [FromForm] IFormFile file)
	{
		if (Request.HasFormContentType && (await Request.ReadFormAsync(HttpContext.RequestAborted)).Files.Count != 1)
			throw new BusinessException("Students:Records:InvalidPhoto");
		if (file == null || file.Length == 0) throw new BusinessException("Students:Records:InvalidPhoto");
		if (file.Length > MaxBytes) throw new BusinessException("Students:Records:PhotoTooLarge");
		await using var source = file.OpenReadStream();
		using var output = new MemoryStream();
		var buffer = new byte[81920];
		int read;
		while ((read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaxBytes + 1 - (int)output.Length)), HttpContext.RequestAborted)) > 0)
		{
			await output.WriteAsync(buffer.AsMemory(0, read), HttpContext.RequestAborted);
			if (output.Length > MaxBytes) throw new BusinessException("Students:Records:PhotoTooLarge");
		}
		return Result.Success(await service.UploadAsync(studentId, new() { FileName = file.FileName, ContentType = file.ContentType, Content = output.ToArray() }));
	}

	[HttpGet("drafts/{draftId:guid}")]
	public async Task<IActionResult> GetDraftAsync(Guid studentId, Guid draftId) => Download(await service.GetDraftAsync(studentId, draftId));
	[HttpPost("changes")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Portal_ProfilePhoto.Add)]
	public async Task<Result<StudentPhotoChangeDto>> SubmitAsync(Guid studentId, [FromBody] SubmitStudentPhotoChangeDto input) => Result.Success(await service.SubmitAsync(studentId, input));
	[HttpGet("changes/{changeId:guid}")]
	public async Task<Result<StudentPhotoChangeDto>> GetChangeAsync(Guid studentId, Guid changeId) => Result.Success(await service.GetChangeAsync(studentId, changeId));
	private FileContentResult Download(StudentPhotoDownloadDto photo)
	{
		Response.Headers.CacheControl = "private, no-store";
		Response.Headers.XContentTypeOptions = "nosniff";
		return File(photo.Content, photo.ContentType);
	}
}
