using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;

namespace ShikshaMitra.Students.Attendance;

[RemoteService(Name = "Students")]
[Area("students")]
[Route("api/students/attendance")]
public class StudentAttendanceController : AbpControllerBase, IStudentAttendanceAppService
{
	private readonly IStudentAttendanceAppService _attendanceAppService;

	public StudentAttendanceController(IStudentAttendanceAppService attendanceAppService)
	{
		_attendanceAppService = attendanceAppService;
	}

    [HttpGet("students")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View)]
	public Task<StudentAttendanceListDto> GetStudentsForDateAsync([FromQuery] GetStudentsForAttendanceInput input)
	{
		return _attendanceAppService.GetStudentsForDateAsync(input);
	}

    [HttpPost("mark")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<MarkAttendanceOutput> MarkAttendanceAsync([FromBody] MarkAttendanceInput input)
	{
		return _attendanceAppService.MarkAttendanceAsync(input);
	}

    [HttpPost("lock")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<LockAttendanceDayOutput> LockAttendanceDayAsync([FromBody] LockAttendanceDayInput input)
	{
		return _attendanceAppService.LockAttendanceDayAsync(input);
	}

    [HttpPost("unlock")]
    [Authorize(ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.Add)]
	public Task<LockAttendanceDayOutput> UnlockAttendanceDayAsync([FromBody] LockAttendanceDayInput input)
	{
		return _attendanceAppService.UnlockAttendanceDayAsync(input);
	}
}
