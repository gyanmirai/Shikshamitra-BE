using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.EmployeeAttendanceContract;
using ShikshaMitra.Models;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

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
		/// Import employee attendance via Excel or CSV file with comprehensive validation
		/// </summary>
		/// <remarks>
		/// **Expected File Format:**
		/// 
		/// | Employee Name | Hours Worked | Time In | Time Out | status | Employee Code | Contact No |
		/// |--------------|--------------|---------|----------|--------|---------------|------------|
		/// | John Doe     | 8.5         | 09:00 AM | 05:30 PM | Present | EMP001       | 9876543210 |
		/// 
		/// **Supported Formats:**
		/// - Excel (.xlsx)
		/// - CSV (.csv)
		/// 
		/// **Features:**
		/// - ✅ Partial success - failed rows don't prevent others from importing
		/// - ✅ User-friendly error messages with suggestions
		/// - ✅ Automatic employee lookup by code
		/// - ✅ Cross-validation of employee name and contact
		/// - ✅ Time format validation with multiple supported formats
		/// - ✅ Duplicate detection within file
		/// - ✅ Hours worked consistency check
		/// - ✅ Status auto-determination from times
		/// 
		/// **Time Format Examples:**
		/// - "09:00 AM" or "09:00"
		/// - "2024-01-15 09:00"
		/// - "09:00:00"
		/// 
		/// **Status Values:**
		/// - Present, HalfDay, Absent, Leave (case-insensitive)
		/// </remarks>
		/// <param name="file">Excel (.xlsx) or CSV (.csv) attendance file</param>
		/// <param name="date">Attendance date</param>
		/// <returns>Detailed import result with per-row success/failure information</returns>
		[HttpPost("import")]
		[ProducesResponseType(typeof(AttendanceImportResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public virtual async Task<ActionResult<AttendanceImportResponse>> ImportAsync(
			[FromForm, Required] EmployeeAttendanceImportRequest file,
			CancellationToken cancellationToken)
		{
			using var stream = file.File.OpenReadStream();
			var result = await _attendanceAppService.ImportAttendanceAsync(
				file.Date, stream, file.File.FileName, cancellationToken);
			return Ok(result);
		}
	}

