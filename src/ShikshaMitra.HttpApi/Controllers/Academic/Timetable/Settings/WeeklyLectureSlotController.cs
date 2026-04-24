using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Timetable.Settings;

[Route("api/academics/timetable/settings/weekly-lecture-slots")]
[Authorize]
[ApiController]
public class WeeklyLectureSlotController(
    IWeeklyLectureSlotAppService weeklyLectureSlotAppService
) : ShikshaMitraController
{
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View)]
    public async Task<PagedResultDto<WeeklyLectureSlotListDto>> GetListAsync(
        [FromQuery] GetWeeklyLectureSlotsInput input)
    {
        return await weeklyLectureSlotAppService.GetListAsync(input);
    }

    [HttpGet("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.View)]
    public async Task<WeeklyLectureSlotDto> GetAsync([FromRoute] Guid id)
    {
        return await weeklyLectureSlotAppService.GetAsync(id);
    }

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.Add)]
    public async Task<WeeklyLectureSlotDto> CreateAsync(
        [FromQuery] Guid academicMediumId,
        [FromBody] CreateWeeklyLectureSlotDto input)
    {
        return await weeklyLectureSlotAppService.CreateAsync(academicMediumId, input);
    }

    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_SettingsWeeklyLectures.Add)]
    public async Task<WeeklyLectureSlotDto> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateWeeklyLectureSlotDto input)
    {
        return await weeklyLectureSlotAppService.UpdateAsync(id, input);
    }
}
