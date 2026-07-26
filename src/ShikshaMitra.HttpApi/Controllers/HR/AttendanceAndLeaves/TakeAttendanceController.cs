using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr/attendance/take-attendance")]
[Authorize]
public class TakeAttendanceController(ITakeAttendanceAppService takeAttendanceAppService) : AbpControllerBase
{
	[HttpGet("day")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.View)]
	[ProducesResponseType(typeof(TakeAttendanceDayResponse), StatusCodes.Status200OK)]
	public virtual Task<TakeAttendanceDayResponse> GetDayAsync(
		[FromQuery, Required] string attendanceDate,
		[FromQuery] string? department = null,
		[FromQuery] bool? isTeaching = null,
		[FromQuery] Guid? roleId = null,
		[FromQuery] string? search = null,
		[FromQuery, Range(0, int.MaxValue)] int skipCount = 0,
		[FromQuery, Range(1, 500)] int maxResultCount = 50)
	{
		return takeAttendanceAppService.GetDayAsync(new GetTakeAttendanceDayInput
		{
			AttendanceDate = attendanceDate,
			Department = department,
			IsTeaching = isTeaching,
			RoleId = roleId,
			Search = search,
			SkipCount = skipCount,
			MaxResultCount = maxResultCount
		});
	}

	[HttpPost("day/save")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Add)]
	[ProducesResponseType(typeof(TakeAttendanceSaveOutput), StatusCodes.Status200OK)]
	public virtual Task<TakeAttendanceSaveOutput> SaveDayAsync([FromBody, Required] TakeAttendanceSaveInput input)
	{
		return takeAttendanceAppService.SaveDayAsync(input);
	}

	[HttpPost("day/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Delete)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> LockDayAsync(
		[FromQuery, Required] string attendanceDate)
	{
		await takeAttendanceAppService.LockDayAsync(attendanceDate);
		return NoContent();
	}

	[HttpDelete("day/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_MarkAttendance.Delete)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public virtual async Task<IActionResult> UnlockDayAsync(
		[FromQuery, Required] string attendanceDate)
	{
		await takeAttendanceAppService.UnlockDayAsync(attendanceDate);
		return NoContent();
	}
}
