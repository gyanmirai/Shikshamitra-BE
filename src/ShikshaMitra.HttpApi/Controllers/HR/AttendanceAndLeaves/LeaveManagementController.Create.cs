using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using ShikshaMitra.HttpApi.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

public partial class LeaveManagementController
{
	/// <summary>
	/// Create leave request (multipart: leavePolicyId, dates, reason, optional employeeId for on-behalf, optional attachment).
	/// </summary>
	[HttpPost]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(LeaveRequestCreatedResponse), StatusCodes.Status201Created)]
	public virtual async Task<ActionResult<LeaveRequestCreatedResponse>> CreateAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromForm] Guid? leavePolicyId,
		[FromForm] Guid? forEmployeeId,
		[FromForm] Guid? employeeId,
		[FromForm, Required] DateTime startDate,
		[FromForm, Required] DateTime endDate,
		[FromForm] bool halfDay,
		[FromForm, Required, MinLength(5), MaxLength(500)] string reason,
		[FromForm] EmployeeLeaveCreateUploadRequest? attachment)
	{
		if (!leavePolicyId.HasValue || leavePolicyId.Value == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		var createDto = new LeaveRequestCreateDto
		{
			LeavePolicyId = leavePolicyId.Value,
			ForEmployeeId = forEmployeeId ?? employeeId,
			StartDate = startDate,
			EndDate = endDate,
			HalfDay = halfDay,
			Reason = reason
		};
		var file = attachment?.File;
		var created = file is null
			? await _leaveManagementAppService.CreateAsync(academicYearId, createDto)
			: await _leaveManagementAppService.CreateAsync(academicYearId, createDto, file);
		return StatusCode(StatusCodes.Status201Created, LeaveManagementContractMapper.ToCreatedResponse(created));
	}

	/// <summary>
	/// Create leave request for the signed-in employee only (no on-behalf support).
	/// </summary>
	[HttpPost("my")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(LeaveRequestCreatedResponse), StatusCodes.Status201Created)]
	public virtual async Task<ActionResult<LeaveRequestCreatedResponse>> CreateMyAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromForm] Guid? leavePolicyId,
		[FromForm, Required] DateTime startDate,
		[FromForm, Required] DateTime endDate,
		[FromForm] bool halfDay,
		[FromForm, Required, MinLength(5), MaxLength(500)] string reason,
		[FromForm] EmployeeLeaveCreateUploadRequest? attachment)
	{
		if (!leavePolicyId.HasValue || leavePolicyId.Value == Guid.Empty)
		{
			throw new BusinessException(HRErrorCodes.LeavePolicyNotFound);
		}

		var createDto = new LeaveRequestCreateDto
		{
			LeavePolicyId = leavePolicyId.Value,
			StartDate = startDate,
			EndDate = endDate,
			HalfDay = halfDay,
			Reason = reason
		};

		var file = attachment?.File;
		var created = file is null
			? await _leaveManagementAppService.CreateMyAsync(academicYearId, createDto)
			: await _leaveManagementAppService.CreateMyAsync(academicYearId, createDto, file);
		return StatusCode(StatusCodes.Status201Created, LeaveManagementContractMapper.ToCreatedResponse(created));
	}
}
