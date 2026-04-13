using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Attendance;

namespace ShikshaMitra.HttpApi.Controllers.Student.Attendance;

[RemoteService(Name = "Students")]
[Area("students")]
[Route("api/students/attendance")]
public class StudentAttendanceController(IStudentAttendanceAppService attendanceAppService) : AbpControllerBase
{
    [HttpGet("students")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public Task<StudentAttendanceListDto> GetStudentsForDateAsync([FromQuery] GetStudentsForAttendanceInput input)
	{
		return attendanceAppService.GetStudentsForDateAsync(input);
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
