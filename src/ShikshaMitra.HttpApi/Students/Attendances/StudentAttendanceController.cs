using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Students.Attendances;

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
	public Task<StudentAttendanceListDto> GetStudentsForDateAsync([FromQuery] GetStudentsForAttendanceInput input)
	{
		return _attendanceAppService.GetStudentsForDateAsync(input);
	}

	[HttpPost("mark")]
	public Task<MarkAttendanceOutput> MarkAttendanceAsync([FromBody] MarkAttendanceInput input)
	{
		return _attendanceAppService.MarkAttendanceAsync(input);
	}

	[HttpPost("lock")]
	public Task<LockAttendanceDayOutput> LockAttendanceDayAsync([FromBody] LockAttendanceDayInput input)
	{
		return _attendanceAppService.LockAttendanceDayAsync(input);
	}

	[HttpPost("unlock")]
	public Task<LockAttendanceDayOutput> UnlockAttendanceDayAsync([FromBody] LockAttendanceDayInput input)
	{
		return _attendanceAppService.UnlockAttendanceDayAsync(input);
	}
}
