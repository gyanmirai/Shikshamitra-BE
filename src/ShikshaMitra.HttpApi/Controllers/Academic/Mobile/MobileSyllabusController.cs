using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;

namespace ShikshaMitra.Controllers.Academic.Mobile;

[ApiController, Authorize]
[Route("api/mobile/v1/academics/syllabus")]
public class MobileSyllabusController(IMobileSyllabusAppService service) : ShikshaMitraController
{
	[HttpGet("dashboard")] public async Task<Result<MobileSyllabusDto>> Dashboard([FromQuery] MobileAcademicInput input) => Result.Success(await service.GetDashboardAsync(input));
	[HttpGet("subjects/{subjectId:guid}/chapters")] public async Task<Result<MobilePage<MobileChapterDto>>> Chapters(Guid subjectId, [FromQuery] MobileListInput input) => Result.Success(await service.GetChaptersAsync(subjectId, input));
	[HttpGet("chapters/{id:guid}")] public async Task<Result<MobileChapterDto>> Chapter(Guid id, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetChapterAsync(id, input));
	[HttpGet("chapters/{chapterId:guid}/resources/{resourceId:guid}/download")]
	public async Task<IActionResult> Download(Guid chapterId, Guid resourceId, [FromQuery] MobileAcademicInput input)
	{
		var file = await service.DownloadAsync(chapterId, resourceId, input);
		return File(file.Content, file.ContentType, file.Name);
	}
}
