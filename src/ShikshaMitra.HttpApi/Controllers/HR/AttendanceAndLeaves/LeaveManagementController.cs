using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HttpApi.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/leave-management")]
[Authorize]
public class LeaveManagementController : AbpControllerBase
{
	private const string ExcelContentType =
		"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	private readonly ILeaveManagementAppService _leaveManagementAppService;

	public LeaveManagementController(ILeaveManagementAppService leaveManagementAppService)
	{
		_leaveManagementAppService = leaveManagementAppService;
	}

	/// <summary>
	/// Get leave requests filtered by academic year, calendar month (from month-year), and optional search.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(LeaveManagementListResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveManagementListResponse> GetListAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery(Name = "month-year"), Required] DateTime monthYear,
		[FromQuery] string? search = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] Guid? leaveTypeMasterId = null)
	{
		var input = new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = monthYear,
			Search = search,
			Page = page,
			PageSize = pageSize,
			Status = ParseStatus(status),
			LeavePolicyId = leavePolicyId,
			LeaveTypeMasterId = leaveTypeMasterId
		};
		var result = await _leaveManagementAppService.GetListAsync(input);
		return LeaveManagementContractMapper.ToListResponse(result, page, pageSize);
	}

	/// <summary>
	/// Per-policy leave balances for an employee (defaults to the signed-in user's employee).
	/// </summary>
	[HttpGet("balances")]
	[ProducesResponseType(typeof(LeaveBalanceListDto), StatusCodes.Status200OK)]
	public virtual async Task<LeaveBalanceListDto> GetBalancesAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery] Guid? employeeId = null)
	{
		return await _leaveManagementAppService.GetBalancesAsync(academicYearId, employeeId);
	}

	/// <summary>
	/// Leave requests overlapping a date range (calendar view).
	/// </summary>
	[HttpGet("calendar")]
	[ProducesResponseType(typeof(LeaveManagementCalendarResultDto), StatusCodes.Status200OK)]
	public virtual async Task<LeaveManagementCalendarResultDto> GetCalendarAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery, Required] DateTime rangeStart,
		[FromQuery, Required] DateTime rangeEnd,
		[FromQuery] string? search = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 100,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] Guid? leaveTypeMasterId = null)
	{
		var input = new LeaveManagementCalendarInput
		{
			AcademicYearId = academicYearId,
			RangeStart = rangeStart,
			RangeEnd = rangeEnd,
			Search = search,
			Page = page,
			PageSize = pageSize,
			Status = ParseStatus(status),
			LeavePolicyId = leavePolicyId,
			LeaveTypeMasterId = leaveTypeMasterId
		};
		return await _leaveManagementAppService.GetCalendarAsync(input);
	}

	/// <summary>
	/// Export leave requests for the same month filters as GET list (Excel).
	/// </summary>
	[HttpGet("export")]
	public virtual async Task<FileContentResult> ExportAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery(Name = "month-year"), Required] DateTime monthYear,
		[FromQuery] string? search = null,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] Guid? leaveTypeMasterId = null)
	{
		var input = new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = monthYear,
			Search = search,
			Page = 1,
			PageSize = 1000,
			Status = ParseStatus(status),
			LeavePolicyId = leavePolicyId,
			LeaveTypeMasterId = leaveTypeMasterId
		};
		var bytes = await _leaveManagementAppService.ExportToExcelAsync(input);
		return new FileContentResult(bytes, ExcelContentType) { FileDownloadName = "leave-requests.xlsx" };
	}

	/// <summary>
	/// Create leave request (multipart: leavePolicyId or leaveTypeId as policy id, dates, reason, optional employeeId for on-behalf, optional attachment).
	/// </summary>
	[HttpPost]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(LeaveRequestCreatedResponse), StatusCodes.Status201Created)]
	public virtual async Task<ActionResult<LeaveRequestCreatedResponse>> CreateAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromForm] Guid? leavePolicyId,
		[FromForm] Guid? leaveTypeId,
		[FromForm] Guid? employeeId,
		[FromForm, Required] DateTime startDate,
		[FromForm, Required] DateTime endDate,
		[FromForm, Required, MinLength(5), MaxLength(500)] string reason,
		[FromForm] EmployeeLeaveCreateUploadRequest? attachment)
	{
		if (!leavePolicyId.HasValue && (!leaveTypeId.HasValue || leaveTypeId.Value == Guid.Empty))
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		var createDto = new LeaveRequestCreateDto
		{
			LeavePolicyId = leavePolicyId,
			LeaveTypeId = leaveTypeId ?? Guid.Empty,
			ForEmployeeId = employeeId,
			StartDate = startDate,
			EndDate = endDate,
			Reason = reason
		};
		var attachmentObj = attachment?.File != null ? attachment.File : null;
		var created = await _leaveManagementAppService.CreateAsync(academicYearId, createDto, attachmentObj);
		return StatusCode(StatusCodes.Status201Created, LeaveManagementContractMapper.ToCreatedResponse(created));
	}

	/// <summary>
	/// Get leave request by ID
	/// </summary>
	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(LeaveRequestDetailsResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveRequestDetailsResponse> GetAsync([FromRoute, Required] Guid id)
	{
		var details = await _leaveManagementAppService.GetAsync(id);
		return LeaveManagementContractMapper.ToDetailsResponse(details);
	}

	/// <summary>
	/// Download leave attachment metadata (JSON). Use /attachment/download for raw file bytes.
	/// </summary>
	[HttpGet("{id:guid}/attachment")]
	[ProducesResponseType(typeof(LeaveAttachmentDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveAttachmentDto> GetAttachmentAsync([FromRoute, Required] Guid id)
	{
		return await _leaveManagementAppService.GetAttachmentAsync(id);
	}

	/// <summary>
	/// Download leave attachment as a file
	/// </summary>
	[HttpGet("{id:guid}/attachment/download")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> DownloadAttachmentAsync([FromRoute, Required] Guid id)
	{
		var attachment = await _leaveManagementAppService.GetAttachmentAsync(id);
		return File(attachment.FileContent, attachment.ContentType, attachment.FileName);
	}

	/// <summary>
	/// Approve a pending (or reviewed) leave request
	/// </summary>
	[HttpPatch("{id:guid}/approve")]
	[ProducesResponseType(typeof(LeaveActionResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveActionResponse> ApproveAsync(
		[FromRoute, Required] Guid id,
		[FromBody] LeaveApprovalRequestDto? body)
	{
		var result = await _leaveManagementAppService.ApproveAsync(id, body);
		return LeaveManagementContractMapper.ToActionResponse(result);
	}

	/// <summary>
	/// Reject a pending (or reviewed) leave request
	/// </summary>
	[HttpPatch("{id:guid}/reject")]
	[ProducesResponseType(typeof(LeaveActionResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveActionResponse> RejectAsync(
		[FromRoute, Required] Guid id,
		[FromBody, Required] LeaveRejectionRequestDto body)
	{
		var result = await _leaveManagementAppService.RejectAsync(id, body);
		return LeaveManagementContractMapper.ToActionResponse(result);
	}

	/// <summary>
	/// Cancel leave request (soft delete)
	/// </summary>
	[HttpDelete("{id:guid}")]
	[ProducesResponseType(typeof(LeaveRequestDeleteResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveRequestDeleteResponse> DeleteAsync([FromRoute, Required] Guid id)
	{
		var deleted = await _leaveManagementAppService.DeleteAsync(id);
		return LeaveManagementContractMapper.ToDeleteResponse(deleted);
	}

	private static EmployeeLeaveStatusType? ParseStatus(string? status)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return null;
		}

		return Enum.TryParse<EmployeeLeaveStatusType>(status.Trim(), true, out var parsed)
			? parsed
			: throw new BusinessException(HRErrorCodes.LeaveInvalidStatus);
	}
}
