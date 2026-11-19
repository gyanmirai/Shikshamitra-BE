using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Mobile;
using ShikshaMitra.Common.Responses;

namespace ShikshaMitra.Controllers.Academic.Mobile;

[ApiController, Authorize]
[Route("api/mobile/v1/academics")]
public class MobileAcademicController(IMobileAcademicContextAppService context, IMobileCalendarAppService calendar,
	IMobileDashboardAppService dashboard) : ShikshaMitraController
{
	[HttpGet("context")] public async Task<Result<MobileContextDto>> Context([FromQuery] MobileAcademicInput input) => Result.Success(await context.GetAsync(input));
	[HttpGet("dashboard")] public async Task<Result<MobileDashboardDto>> Dashboard([FromQuery] MobileAcademicInput input) => Result.Success(await dashboard.GetAsync(input));
	[HttpGet("holidays-events")] public async Task<Result<List<MobileSchoolMarkerDto>>> Calendar([FromQuery] MobileCalendarInput input) => Result.Success(await calendar.GetAsync(input));
	[HttpGet("holidays-events/{id:guid}")] public async Task<Result<MobileSchoolMarkerDto>> Marker(Guid id, [FromQuery] MobileAcademicInput input) => Result.Success(await calendar.GetMarkerAsync(id, input));
}
