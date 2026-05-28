using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[Route("api/academics/timetable")]
[Authorize]
[ApiController]
public class TimetableController(
    ITimetableAppService timetableAppService
) : ShikshaMitraController
{
    [HttpGet("by-standard/{standardId}/division/{divisionId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<TimetableResponseDto> GetByStandardAndDivisionAsync(
        [FromRoute] Guid standardId,
        [FromRoute] Guid divisionId)
    {
        return await timetableAppService.GetByStandardAndDivisionAsync(standardId, divisionId);
    }

    [HttpPost("by-standard/{standardId}/division/{divisionId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task<IActionResult> SaveAsync(
        [FromRoute] Guid standardId,
        [FromRoute] Guid divisionId,
        [FromBody] SaveTimetableDto input)
    {
        await timetableAppService.SaveAsync(standardId, divisionId, input);
        return Ok(new { message = "Timetable saved successfully" });
    }

    [HttpPost("assign-teachers")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task<IActionResult> AssignTeachersAsync([FromBody] AssignTeachersDto input)
    {
        await timetableAppService.AssignTeachersAsync(input);
        return Ok(new { message = "Teachers assigned successfully" });
    }

    [HttpPost("assign-rooms")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public async Task<IActionResult> AssignRoomsAsync([FromBody] AssignRoomsDto input)
    {
        await timetableAppService.AssignRoomsAsync(input);
        return Ok(new { message = "Rooms assigned successfully" });
    }

    [HttpGet("slot-config/by-standard/{standardId}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<SlotConfigResponseDto> GetSlotConfigByStandardAsync(
        [FromRoute] Guid standardId)
    {
        return await timetableAppService.GetSlotConfigByStandardAsync(standardId);
    }

    [HttpGet("by-standard/{standardId}/division/{divisionId}/report/pdf")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public async Task<IActionResult> GetClasswiseTimetablePdfAsync(
        [FromRoute] Guid standardId,
        [FromRoute] Guid divisionId)
    {
        var pdfBytes = await timetableAppService.GetClasswiseTimetablePdfAsync(standardId, divisionId);
        return File(pdfBytes, "application/pdf", $"timetable-{standardId}-{divisionId}.pdf");
    }
}
