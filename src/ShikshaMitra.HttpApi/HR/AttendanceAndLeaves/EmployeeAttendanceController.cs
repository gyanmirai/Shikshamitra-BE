using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.EmployeeAttendanceContract;
using ShikshaMitra.Models;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// API Controller for employee attendance management
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("attendance")]
[Authorize]
public class EmployeeAttendanceController : AbpControllerBase
{
	private readonly IEmployeeAttendanceContractAppService _attendanceAppService;

	public EmployeeAttendanceController(IEmployeeAttendanceContractAppService attendanceAppService)
	{
		_attendanceAppService = attendanceAppService;
	}

	/// <summary>
	/// Get paginated employee attendance by date
	/// </summary>
	/// <param name="date">Attendance date (ISO date-time)</param>
	/// <param name="page">Page index (0-based)</param>
	/// <param name="pageSize">Records per page (10-100)</param>
	/// <returns>Paginated attendance list</returns>
	[HttpGet("employees")]
	[ProducesResponseType(typeof(PaginatedAttendanceResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<ActionResult<PaginatedAttendanceResponse>> GetByDateAsync(
		[FromQuery, Required] DateTimeOffset date,
		[FromQuery, Range(0, int.MaxValue)] int page = 0,
		[FromQuery, Range(10, 100)] int pageSize = 20)
	{
		var input = new GetPaginatedAttendanceInput
		{
			Date = date,
			Page = page,
			PageSize = pageSize
		};

		var result = await _attendanceAppService.GetPaginatedByDateAsync(input);
		return Ok(result);
	}

	/// <summary>
	/// Update employee attendance
	/// </summary>
	/// <param name="request">Update attendance request</param>
	/// <returns>Success message</returns>
	[HttpPut("employees")]
	[ProducesResponseType(typeof(SuccessMessage), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<ActionResult<SuccessMessage>> UpdateAsync(
		[FromBody, Required] UpdateAttendanceRequest request)
	{
		var result = await _attendanceAppService.UpdateAttendanceAsync(request);
		return Ok(result);
	}

	/// <summary>
	/// Import employee attendance via Excel
	/// </summary>
	/// <param name="file">Excel (.xlsx) attendance file</param>
	/// <param name="date">Attendance date</param>
	/// <returns>Import result with success/failure counts</returns>
	[HttpPost("import")]
	[ProducesResponseType(typeof(AttendanceImportResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public virtual async Task<ActionResult<AttendanceImportResponse>> ImportAsync(
		[FromForm, Required] EmployeeAttendanceImportRequest file)
	{
		using var stream = file.File.OpenReadStream();
		var result = await _attendanceAppService.ImportAttendanceAsync(file.Date, stream, file.File.FileName);
		return Ok(result);
	}
}

