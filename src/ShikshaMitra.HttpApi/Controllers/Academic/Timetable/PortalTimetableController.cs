using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Portal;
using ShikshaMitra.Common.Responses;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[ApiController]
[Authorize]
[Route("api/mobile/v1/academics/timetable")]
public class PortalTimetableController(IPortalTimetableAppService service) : ShikshaMitraController
{
    [HttpGet("context")]
    public async Task<Result<PortalTimetableContextDto>> GetContextAsync(
        [FromQuery] Guid? studentId = null, [FromQuery] Guid? academicYearId = null) =>
        Result.Success(await service.GetContextAsync(studentId, academicYearId));

    [HttpGet("schedule")]
    public async Task<Result<PortalTimetableScheduleDto>> GetScheduleAsync(
        [FromQuery] PortalTimetableScheduleInput input) => Result.Success(await service.GetScheduleAsync(input));
}
