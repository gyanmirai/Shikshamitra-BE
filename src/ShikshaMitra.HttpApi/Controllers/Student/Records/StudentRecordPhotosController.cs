using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records/changes/students/{studentId}/photo-drafts")]
public class StudentRecordPhotosController(IStudentRecordPhotoAppService photos) : AbpController
{
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.Create)]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(4 * 1024 * 1024)]
	public async Task<StudentRecordPhotoDraftDto> Upload(Guid studentId, IFormFile file)
	{
		if (file.Length == 0 || file.Length > 3 * 1024 * 1024) throw new BadHttpRequestException("Choose a photo up to 3 MB.");
		using var content = new MemoryStream();
		await file.CopyToAsync(content, HttpContext.RequestAborted);
		return await photos.UploadAsync(studentId, new() { FileName = file.FileName, ContentType = file.ContentType, Content = content.ToArray() });
	}
	[HttpGet("{draftId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Changes.View)]
	public async Task<IActionResult> Get(Guid studentId, Guid draftId)
	{
		var photo = await photos.GetAsync(studentId, draftId);
		return File(photo.Content, photo.ContentType, photo.FileName);
	}
}
