using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;

namespace ShikshaMitra.Controllers.Academic.Mobile;

[ApiController, Authorize]
[Route("api/mobile/v1/exams")]
public class MobileExamController(IMobileExamAppService service) : ShikshaMitraController
{
	[HttpGet] public async Task<Result<MobilePage<MobileExamDto>>> List([FromQuery] MobileListInput input) => Result.Success(await service.GetListAsync(input));
	[HttpGet("{id:guid}")] public async Task<Result<MobileExamDto>> Detail(Guid id, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetAsync(id, input));
	[HttpGet("{id:guid}/subjects/{scheduleId:guid}/preparation")] public async Task<Result<MobileExamSubjectDto>> Preparation(Guid id, Guid scheduleId, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetPreparationAsync(id, scheduleId, input));
	[HttpGet("{id:guid}/subjects/{scheduleId:guid}/seating-plan")] public async Task<Result<MobileSeatingDto>> Seating(Guid id, Guid scheduleId, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetSeatingAsync(id, scheduleId, input));
	[HttpGet("{id:guid}/subjects/{scheduleId:guid}/syllabus-portion")] public async Task<Result<List<MobileAttachmentDto>>> Portion(Guid id, Guid scheduleId, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetResourcesAsync(id, scheduleId, input, "SyllabusPortion"));
	[HttpGet("{id:guid}/subjects/{scheduleId:guid}/paper-pattern")] public async Task<Result<List<MobileAttachmentDto>>> Pattern(Guid id, Guid scheduleId, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetResourcesAsync(id, scheduleId, input, "PaperPattern"));
	[HttpGet("{id:guid}/subjects/{scheduleId:guid}/question-banks")] public async Task<Result<List<MobileAttachmentDto>>> Questions(Guid id, Guid scheduleId, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetResourcesAsync(id, scheduleId, input, "QuestionBank"));
	[HttpGet("{id:guid}/subjects/{scheduleId:guid}/resources/{resourceId:guid}/download")]
	public async Task<IActionResult> Download(Guid id, Guid scheduleId, Guid resourceId, [FromQuery] MobileAcademicInput input)
	{
		var file = await service.DownloadAsync(id, scheduleId, resourceId, input);
		return File(file.Content, file.ContentType, file.Name);
	}
}
