using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

public partial class LeaveManagementController
{
	/// <summary>
	/// Get leave requests filtered by academic year. When <c>month-year</c> is set, results are limited to that calendar month (clipped to the year). When omitted, the full academic year span is used.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(LeaveManagementListResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveManagementListResponse> GetListAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery(Name = "month-year")] DateTime? monthYear,
		[FromQuery] string? search = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 10,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] string? paidType = null,
		[FromQuery] Guid? employeeId = null)
	{
		var input = new LeaveManagementGetListInput
		{
			AcademicYearId = academicYearId,
			MonthYearAnchor = monthYear,
			Search = search,
			Page = page,
			PageSize = pageSize,
			Status = ParseStatus(status),
			EmployeeId = employeeId,
			LeavePolicyId = leavePolicyId,
			PaidType = paidType
		};
		var result = await _leaveManagementAppService.GetListAsync(input);
		return LeaveManagementContractMapper.ToListResponse(result, page, pageSize);
	}

	/// <summary>
	/// Get only the signed-in employee leave requests.
	/// </summary>
	[HttpGet("my")]
	[ProducesResponseType(typeof(LeaveManagementListResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveManagementListResponse> GetMyListAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery(Name = "month-year")] DateTime? monthYear,
		[FromQuery] string? search = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 10,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] string? paidType = null)
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
			PaidType = paidType
		};
		var result = await _leaveManagementAppService.GetMyListAsync(input);
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
	/// Per-policy leave balances for the signed-in employee only.
	/// </summary>
	[HttpGet("my/balances")]
	[ProducesResponseType(typeof(LeaveBalanceListDto), StatusCodes.Status200OK)]
	public virtual async Task<LeaveBalanceListDto> GetMyBalancesAsync(
		[FromQuery, Required] Guid academicYearId)
	{
		return await _leaveManagementAppService.GetMyBalancesAsync(academicYearId);
	}

	/// <summary>
	/// Leave requests overlapping a date range (calendar view). Accepts ISO-8601 date-time values with timezone offset.
	/// </summary>
	[HttpGet("calendar")]
	[ProducesResponseType(typeof(LeaveManagementCalendarResultDto), StatusCodes.Status200OK)]
	public virtual async Task<LeaveManagementCalendarResultDto> GetCalendarAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery, Required] DateTimeOffset rangeStart,
		[FromQuery, Required] DateTimeOffset rangeEnd,
		[FromQuery] string? search = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 100,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] string? paidType = null)
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
			PaidType = paidType
		};
		return await _leaveManagementAppService.GetCalendarAsync(input);
	}

	/// <summary>
	/// Export leave requests for the same month filters as GET list (Excel).
	/// </summary>
	[HttpGet("export")]
	public virtual async Task<FileContentResult> ExportAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery(Name = "month-year")] DateTime? monthYear,
		[FromQuery] string? search = null,
		[FromQuery] string? status = null,
		[FromQuery] Guid? leavePolicyId = null,
		[FromQuery] string? paidType = null)
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
			PaidType = paidType
		};
		var bytes = await _leaveManagementAppService.ExportToExcelAsync(input);
		return new FileContentResult(bytes, ExcelContentType) { FileDownloadName = "leave-requests.xlsx" };
	}
}
