using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Timetable.Settings;

[Route("api/academics/timetable/settings/lecture-slots")]
[Authorize]
[ApiController]
public class LectureSlotController(
    ILectureSlotAppService lectureSlotAppService
) : ShikshaMitraController
{
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<PagedResultDto<LectureSlotListDto>> GetByMediumAsync(
        [FromQuery] GetLectureSlotsByMediumInput input)
    {
        return await lectureSlotAppService.GetByMediumAsync(input);
    }

    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.View)]
    public async Task<LectureSlotDto> GetAsync([FromRoute] Guid id)
    {
        return await lectureSlotAppService.GetAsync(id);
    }

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<LectureSlotDto> CreateAsync([FromBody] CreateLectureSlotDto input)
    {
        return await lectureSlotAppService.CreateAsync(input);
    }

    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<LectureSlotDto> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateLectureSlotDto input)
    {
        return await lectureSlotAppService.UpdateAsync(id, input);
    }

    [HttpPost("{id}/assign")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsLectureSlots.Add)]
    public async Task<IActionResult> AssignAsync(
        [FromRoute] Guid id,
        [FromBody] AssignLectureSlotDto input)
    {
        await lectureSlotAppService.AssignAsync(id, input);
        return Ok(new { message = "Lecture slot assigned successfully" });
    }
}
