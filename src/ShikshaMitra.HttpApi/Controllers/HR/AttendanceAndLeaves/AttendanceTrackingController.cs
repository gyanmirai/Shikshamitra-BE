using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceTracking;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Validation;

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
	/// <param name="status">Attendance status filter (PRESENT, ABSENT, HALF_DAY, REJECTED_LEAVE)</param>
	/// <param name="page">Page number (1-based)</param>
	/// <param name="pageSize">Page size (10-100)</param>
	/// <param name="educationMediumStageId">Education medium stage id</param>
	/// <param name="search">Search text for employee fields</param>
	/// <returns>Paginated attendance records with summary</returns>
	[HttpGet("date-wise")]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View)]
[ProducesResponseType(typeof(AttendanceDateWiseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public virtual async Task<ActionResult<AttendanceDateWiseResponse>> GetDateWiseAsync(
		[FromQuery, Required] string attendanceDate,
		[FromQuery, Required] string status,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10,
		[FromQuery, Required] Guid educationMediumStageId = default,
		[FromQuery] string? search = null)
	{
		if (!TryParseStatus(status, out var parsedStatus))
		{
			throw CreateStatusValidationException();
		}

		var input = new GetAttendanceDateWiseInput
		{
			AttendanceDate = attendanceDate,
			Status = parsedStatus,
			EducationMediumStageId = educationMediumStageId,
			Search = search,
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
	/// <param name="page">Page number (1-based)</param>
	/// <param name="pageSize">Page size (10-100)</param>
	/// <param name="educationMediumStageId">Education medium stage id</param>
	/// <param name="search">Search text</param>
	/// <returns>Employee attendance records with summary</returns>
	[HttpGet("employee-wise")]
[Authorize(ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View)]
[ProducesResponseType(typeof(AttendanceEmployeeWiseResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	public virtual async Task<ActionResult<AttendanceEmployeeWiseResponse>> GetEmployeeWiseAsync(
		[FromQuery, Required] Guid employeeId,
		[FromQuery, Required] string startDate,
		[FromQuery, Required] string endDate,
		[FromQuery] string? status = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10,
		[FromQuery, Required] Guid educationMediumStageId = default,
		[FromQuery] string? search = null)
	{
		AttendanceTrackingStatus? parsedStatus = null;
		if (!string.IsNullOrWhiteSpace(status))
		{
			if (!TryParseStatus(status, out var statusValue))
			{
				throw CreateStatusValidationException();
			}

			parsedStatus = statusValue;
		}

		var input = new GetAttendanceEmployeeWiseInput
		{
			EmployeeId = employeeId,
			StartDate = startDate,
			EndDate = endDate,
			Status = parsedStatus,
			EducationMediumStageId = educationMediumStageId,
			Search = search,
			Page = page,
			PageSize = pageSize
		};

		var result = await _attendanceTrackingAppService.GetEmployeeWiseAsync(input);
		return Ok(result);
	}

	private static bool TryParseStatus(string status, out AttendanceTrackingStatus parsedStatus)
	{
		parsedStatus = default;
		var normalized = status.Trim().ToUpper(CultureInfo.InvariantCulture);
		return normalized switch
		{
			"PRESENT" => SetStatus(AttendanceTrackingStatus.Present, out parsedStatus),
			"ABSENT" => SetStatus(AttendanceTrackingStatus.Absent, out parsedStatus),
			"HALF_DAY" => SetStatus(AttendanceTrackingStatus.HalfDay, out parsedStatus),
			"REJECTED_LEAVE" => SetStatus(AttendanceTrackingStatus.RejectedLeave, out parsedStatus),
			_ => false
		};
	}

	private static bool SetStatus(AttendanceTrackingStatus value, out AttendanceTrackingStatus parsedStatus)
	{
		parsedStatus = value;
		return true;
	}

	private static AbpValidationException CreateStatusValidationException()
	{
		var validationErrors = new List<ValidationResult>
		{
			new(
				"Status must be one of PRESENT, ABSENT, HALF_DAY, or REJECTED_LEAVE.",
				new[] { "status" })
		};

		return new AbpValidationException(
			"Validation failed",
			validationErrors);
	}
}

