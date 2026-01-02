using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

/// <summary>
/// API Controller for employee attendance management.
/// Provides endpoints for viewing, updating, and importing employee attendance records.
/// </summary>
[RemoteService(IsEnabled = true, IsMetadataEnabled = true)]
[Area("app")]
[Route("api/app/attendance")]
[Authorize]
public class EmployeeAttendanceController : AbpControllerBase
{
	private readonly IEmployeeAttendanceAppService _attendanceService;

	/// <summary>
	/// Initializes a new instance of the <see cref="EmployeeAttendanceController"/> class.
	/// </summary>
	/// <param name="attendanceService">The attendance application service.</param>
	public EmployeeAttendanceController(IEmployeeAttendanceAppService attendanceService)
	{
		_attendanceService = attendanceService;
	}

	/// <summary>
	/// Gets paginated employee attendance records for a specific date.
	/// </summary>
	/// <param name="date">The attendance date (ISO 8601 format).</param>
	/// <param name="page">Page index (0-based). Default: 0.</param>
	/// <param name="pageSize">Number of records per page (10-100). Default: 20.</param>
	/// <returns>A paginated list of attendance records.</returns>
	/// <response code="200">Returns the paginated attendance list.</response>
	/// <response code="401">If the user is not authenticated.</response>
	/// <response code="403">If the user lacks permission.</response>
	[HttpGet("employees")]
	[ProducesResponseType(typeof(PaginatedAttendanceResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<PaginatedAttendanceResponse>> GetByDateAsync(
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

		var result = await _attendanceService.GetPaginatedByDateAsync(input);
		return Ok(result);
	}

	/// <summary>
	/// Updates or creates an employee attendance record.
	/// Uses upsert logic based on (EmployeeId, Date).
	/// </summary>
	/// <param name="request">The attendance update request.</param>
	/// <returns>A success message.</returns>
	/// <response code="200">If the attendance was updated successfully.</response>
	/// <response code="400">If the request is invalid.</response>
	/// <response code="401">If the user is not authenticated.</response>
	/// <response code="403">If the user lacks permission.</response>
	/// <response code="404">If the employee is not found.</response>
	[HttpPut("employees")]
	[ProducesResponseType(typeof(AttendanceSuccessResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<ActionResult<AttendanceSuccessResponse>> UpdateAsync(
		[FromBody, Required] UpdateAttendanceRequest request)
	{
		var result = await _attendanceService.UpdateAttendanceAsync(request);
		return Ok(result);
	}

	/// <summary>
	/// Imports employee attendance records from an Excel or CSV file.
	/// </summary>
	/// <remarks>
	/// **Expected File Format:**
	/// 
	/// | Employee Name | Hours Worked | Time In | Time Out | status | Employee Code | Contact No |
	/// |--------------|--------------|---------|----------|--------|---------------|------------|
	/// | John Doe     | 8.5          | 09:00   | 17:30    | Present | EMP001       | 9876543210 |
	/// 
	/// **Supported Formats:** Excel (.xlsx), CSV (.csv)
	/// 
	/// **Features:**
	/// - Partial success: failed rows don't prevent others from importing
	/// - Automatic employee lookup by code
	/// - Time format validation with multiple supported formats
	/// - Duplicate detection within file
	/// </remarks>
	/// <param name="request">The import request containing the file and date.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Detailed import result with per-row success/failure information.</returns>
	/// <response code="200">Returns the import result with detailed per-row status.</response>
	/// <response code="400">If the file format is invalid or no data rows found.</response>
	/// <response code="401">If the user is not authenticated.</response>
	/// <response code="403">If the user lacks permission.</response>
	[HttpPost("import")]
	[ProducesResponseType(typeof(AttendanceImportResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status401Unauthorized)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(52428800)] // 50 MB
	[RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
	public async Task<ActionResult<AttendanceImportResponse>> ImportAsync(
		[FromForm, Required] EmployeeAttendanceImportRequest request,
		CancellationToken cancellationToken = default)
	{
		using var stream = request.File.OpenReadStream();
		var result = await _attendanceService.ImportAttendanceAsync(
			request.Date,
			stream,
			request.File.FileName,
			cancellationToken);

		return Ok(result);
	}
}
