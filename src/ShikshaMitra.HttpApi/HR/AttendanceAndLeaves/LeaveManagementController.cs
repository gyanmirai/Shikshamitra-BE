using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveManagementInfo;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/leave-management")]
[Authorize]
public class LeaveManagementController : AbpControllerBase
{
	private readonly ILeaveManagementAppService _leaveManagementAppService;

	public LeaveManagementController(ILeaveManagementAppService leaveManagementAppService)
	{
		_leaveManagementAppService = leaveManagementAppService;
	}

	/// <summary>
	/// Get leave requests filtered by month and year
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(LeaveManagementListResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveManagementListResponse> GetListAsync(
		[FromQuery, Required] Guid mediumStageId,
		[FromQuery] int? month = null,
		[FromQuery] int? year = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10)
	{
		var result = await _leaveManagementAppService.GetListAsync(mediumStageId, month, year, page, pageSize);
		return LeaveManagementContractMapper.ToListResponse(result, page, pageSize);
	}

	/// <summary>
	/// Get leave request by ID
	/// </summary>
	[HttpGet("{leaveRequestId}")]
	[ProducesResponseType(typeof(LeaveRequestDetailsResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<ActionResult<LeaveRequestDetailsResponse>> GetAsync([FromRoute, Required] string leaveRequestId)
	{
		if (!int.TryParse(leaveRequestId, out var publicId))
		{
			return BadRequest(new { message = "Invalid leave request ID format" });
		}
		var details = await _leaveManagementAppService.GetAsync(publicId);
		return LeaveManagementContractMapper.ToDetailsResponse(details);
	}

	/// <summary>
	/// Update leave request status
	/// </summary>
	[HttpPut("{leaveRequestId}")]
	[ProducesResponseType(typeof(LeaveRequestDetailsResponse), StatusCodes.Status200OK)]
	public virtual async Task<ActionResult<LeaveRequestDetailsResponse>> UpdateAsync(
		[FromRoute, Required] string leaveRequestId,
		[FromBody, Required] LeaveRequestUpdateRequest request)
	{
		if (!int.TryParse(leaveRequestId, out var publicId))
		{
			return BadRequest(new { message = "Invalid leave request ID format" });
		}
		var updated = await _leaveManagementAppService.UpdateAsync(publicId, request);
		return LeaveManagementContractMapper.ToDetailsResponse(updated);
	}

	/// <summary>
	/// Cancel leave request (soft delete)
	/// </summary>
	[HttpDelete("{leaveRequestId}")]
	[ProducesResponseType(typeof(LeaveRequestDeleteResponse), StatusCodes.Status200OK)]
	public virtual async Task<ActionResult<LeaveRequestDeleteResponse>> DeleteAsync([FromRoute, Required] string leaveRequestId)
	{
		if (!int.TryParse(leaveRequestId, out var publicId))
		{
			return BadRequest(new { message = "Invalid leave request ID format" });
		}
		var deleted = await _leaveManagementAppService.DeleteAsync(publicId);
		return LeaveManagementContractMapper.ToDeleteResponse(deleted);
	}
}

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/v1/leave-management")]
[Authorize]
public class LeaveManagementV1Controller : AbpControllerBase
{
	private readonly ILeaveManagementAppService _leaveManagementAppService;

	public LeaveManagementV1Controller(ILeaveManagementAppService leaveManagementAppService)
	{
		_leaveManagementAppService = leaveManagementAppService;
	}

	/// <summary>
	/// Create leave request (with optional attachment)
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(LeaveRequestCreatedResponse), StatusCodes.Status201Created)]
	public virtual async Task<ActionResult<LeaveRequestCreatedResponse>> CreateAsync(
		[FromForm, Required] Guid mediumStageId,
		[FromForm, Required] Guid leaveTypeId,
		[FromForm, Required] DateTime startDate,
		[FromForm, Required] DateTime endDate,
		[FromForm, Required, MinLength(5), MaxLength(500)] string reason,
		[FromForm] IFormFile? attachment)
	{
		var createDto = new LeaveRequestCreateDto
		{
			MediumStageId = mediumStageId,
			LeaveTypeId = leaveTypeId,
			StartDate = startDate,
			EndDate = endDate,
			Reason = reason
		};
		var created = await _leaveManagementAppService.CreateAsync(createDto, attachment as object);
		return StatusCode(StatusCodes.Status201Created, LeaveManagementContractMapper.ToCreatedResponse(created));
	}
}
