using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[Route("api/academics/timetable/management")]
[Authorize]
[ApiController]
public class TimetableManagementController(
    ITimetableAppService timetableAppService
) : ShikshaMitraController
{
    [HttpGet("by-standard/{standardId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableManagementResponseDto> GetByStandardAsync(
        [FromRoute] Guid standardId,
        [FromQuery] GetTimetableManagementInput input)
    {
        return await timetableAppService.GetManagementByStandardAsync(standardId, input);
    }
}
