using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;

namespace ShikshaMitra.Controllers.Academic.Mobile;

[ApiController, Authorize]
[Route("api/mobile/v1/results")]
public class MobileResultController(IMobileResultAppService service) : ShikshaMitraController
{
	[HttpGet("academic-years")] public async Task<Result<List<MobileYearDto>>> Years([FromQuery] MobileAcademicInput input) => Result.Success(await service.GetYearsAsync(input));
	[HttpGet] public async Task<Result<MobilePage<MobileResultDto>>> List([FromQuery] MobileListInput input) => Result.Success(await service.GetListAsync(input));
	[HttpGet("{id:guid}")] public async Task<Result<MobileResultDto>> Detail(Guid id, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetAsync(id, input));
	[HttpGet("{id:guid}/subjects/{subjectId:guid}")] public async Task<Result<MobileSubjectResultDto>> Subject(Guid id, Guid subjectId, [FromQuery] MobileAcademicInput input) => Result.Success(await service.GetSubjectAsync(id, subjectId, input));
	[HttpGet("{id:guid}/report-card/download")]
	public async Task<IActionResult> ReportCard(Guid id, [FromQuery] MobileAcademicInput input)
	{
		var file = await service.DownloadReportCardAsync(id, input);
		return File(file.Content, file.ContentType, file.Name);
	}
}
