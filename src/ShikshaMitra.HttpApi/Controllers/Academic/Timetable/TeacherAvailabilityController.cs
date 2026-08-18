using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Timetable.Substitutions;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Timetable;

[Route("api/academics/timetable/teacher-availability")]
[Authorize]
[ApiController]
public class TeacherAvailabilityController(ITeacherAvailabilityAppService teacherAvailabilityAppService)
    : ShikshaMitraController
{
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<PagedResultDto<TeacherAvailabilityDto>> GetListAsync([FromQuery] GetTeacherAvailabilityInput input)
    {
        return teacherAvailabilityAppService.GetListAsync(input);
    }

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Add)]
    public Task<TeacherAvailabilityDto> CreateAsync([FromBody] CreateTeacherAvailabilityDto input)
    {
        return teacherAvailabilityAppService.CreateAsync(input);
    }

    [HttpPost("affected-lectures")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<AffectedLectureResultDto> GetAffectedLecturesAsync([FromBody] CreateTeacherAvailabilityDto input)
    {
        return teacherAvailabilityAppService.GetAffectedLecturesAsync(input);
    }

    [HttpGet("affected-lectures")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View)]
    public Task<AffectedLectureResultDto> GetAffectedLecturesFromQueryAsync(
        [FromQuery] string? teacherId,
        [FromQuery] string? teacherName,
        [FromQuery] DateTime availabilityDate,
        [FromQuery] string startTime = "09:00",
        [FromQuery] string endTime = "14:15",
        [FromQuery] bool isFullDay = false,
        [FromQuery] string? reason = null)
    {
        Guid? parsedTeacherId = null;
        if (!string.IsNullOrWhiteSpace(teacherId) && Guid.TryParse(teacherId, out var value))
        {
            parsedTeacherId = value;
        }

        var input = new CreateTeacherAvailabilityDto
        {
            TeacherId = parsedTeacherId,
            TeacherName = teacherName ?? string.Empty,
            AvailabilityDate = availabilityDate,
            StartTime = startTime,
            EndTime = endTime,
            IsFullDay = isFullDay,
            Reason = reason ?? "Preview",
            NotifyLeadership = false
        };

        return teacherAvailabilityAppService.GetAffectedLecturesAsync(input);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.Delete)]
    public Task CancelAsync([FromRoute] Guid id)
    {
        return teacherAvailabilityAppService.CancelAsync(id);
    }
}
