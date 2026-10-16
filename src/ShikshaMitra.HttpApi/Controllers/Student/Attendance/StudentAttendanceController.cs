using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Attendance;
using Volo.Abp.Application.Dtos;
using System;

namespace ShikshaMitra.HttpApi.Controllers.Student.Attendance;

[ApiController]
[Authorize]
[Route("api/students/attendance")]
public class StudentAttendanceController(IStudentAttendanceAppService attendanceAppService) : AbpControllerBase
{
	[HttpGet("context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public Task<AttendanceWorkspaceContextDto> GetWorkspaceContextAsync([FromQuery] Guid academicYearId) =>
		attendanceAppService.GetWorkspaceContextAsync(academicYearId);

	[HttpGet("workspace")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public Task<PagedResultDto<StudentAttendanceDto>> GetWorkspaceAsync([FromQuery] AttendanceWorkspaceInput input) =>
		attendanceAppService.GetWorkspaceAsync(input);

    [HttpGet("students")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public Task<StudentAttendanceListDto> GetStudentsForDateAsync([FromQuery] GetStudentsForAttendanceInput input)
	{
		return attendanceAppService.GetStudentsForDateAsync(input);
	}

	[HttpGet("history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public Task<PagedResultDto<StudentAttendanceHistoryDto>> GetAttendanceHistoryAsync(
		[FromQuery] GetStudentAttendanceHistoryInput input)
	{
		return attendanceAppService.GetAttendanceHistoryAsync(input);
	}

    [HttpPost("mark")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<MarkAttendanceOutput> MarkAttendanceAsync([FromBody] MarkAttendanceInput input)
	{
		return attendanceAppService.MarkAttendanceAsync(input);
    }

    [HttpPost("lock")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<LockAttendanceDayOutput> LockAttendanceDayAsync([FromBody] LockAttendanceDayInput input)
	{
		return attendanceAppService.LockAttendanceDayAsync(input);
	}

    [HttpPost("unlock")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<LockAttendanceDayOutput> UnlockAttendanceDayAsync([FromBody] LockAttendanceDayInput input)
	{
		return attendanceAppService.UnlockAttendanceDayAsync(input);
	}
}
