using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

/// <summary>
/// API Controller for attendance tracking (date-wise and employee-wise reports)
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/attendance-tracking")]
[Authorize]
public class AttendanceTrackingController : AbpControllerBase
{
	private readonly IAttendanceTrackingAppService _attendanceTrackingAppService;

	public AttendanceTrackingController(IAttendanceTrackingAppService attendanceTrackingAppService)
	{
		_attendanceTrackingAppService = attendanceTrackingAppService;
	}

	/// <summary>
	/// Get attendance (date-wise) - Fetch attendance records for a specific date
	/// </summary>
	/// <param name="attendanceDate">Attendance date (DD-MM-YYYY format or ISO date-time)</param>
	/// <param name="status">Optional attendance status filter</param>
	/// <param name="page">Page number (1-based)</param>
	/// <param name="pageSize">Page size (10-100)</param>
	/// <returns>Paginated attendance records with summary</returns>
	[HttpGet("date-wise")]
	[ProducesResponseType(typeof(AttendanceDateWiseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public virtual async Task<ActionResult<AttendanceDateWiseResponse>> GetDateWiseAsync(
		[FromQuery, Required] string attendanceDate,
		[FromQuery] AttendanceTrackingStatus? status = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10)
	{
		var input = new GetAttendanceDateWiseInput
		{
			AttendanceDate = attendanceDate,
			Status = status,
			Page = page,
			PageSize = pageSize
		};

		var result = await _attendanceTrackingAppService.GetDateWiseAsync(input);
		return Ok(result);
	}

	/// <summary>
	/// Get attendance (employee-wise) - Fetch attendance records for a specific employee and date range
	/// </summary>
	/// <param name="employeeId">Employee ID (UUID)</param>
	/// <param name="startDate">Start date (DD-MM-YYYY format)</param>
	/// <param name="endDate">End date (DD-MM-YYYY format)</param>
	/// <param name="status">Optional attendance status filter</param>
	/// <returns>Employee attendance records with summary</returns>
	[HttpGet("employee-wise")]
	[ProducesResponseType(typeof(AttendanceEmployeeWiseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public virtual async Task<ActionResult<AttendanceEmployeeWiseResponse>> GetEmployeeWiseAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string startDate,
		[FromQuery, Required] string endDate,
		[FromQuery] AttendanceTrackingStatus? status = null)
	{
		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = employeeId,
			StartDate = startDate,
			EndDate = endDate,
			Status = status
		};

		var result = await _attendanceTrackingAppService.GetEmployeeWiseAsync(input);
		return Ok(result);
	}
}

