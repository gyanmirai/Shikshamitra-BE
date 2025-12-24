using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.LeaveTypeInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Leave type management controller
/// Handles CRUD operations for employee leave types in the school ERP system
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/leave-types")]
[Authorize]
public class LeaveTypesController : AbpControllerBase
{
	private readonly ILeaveTypeAppService _leaveTypeAppService;

	public LeaveTypesController(ILeaveTypeAppService leaveTypeAppService)
	{
		_leaveTypeAppService = leaveTypeAppService;
	}

	/// <summary>
	/// Get paginated list of leave types
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(LeaveTypeListResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveTypeListResponse> GetListAsync(
		[FromQuery, Required] Guid mediumStageId,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10)
	{
		var input = new PagedAndSortedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		};

		var result = await _leaveTypeAppService.GetListAsync(input);
		return LeaveTypeContractMapper.ToListResponse(result, page, pageSize);
	}

	/// <summary>
	/// Get leave type by ID
	/// </summary>
	[HttpGet("{leaveTypeId}")]
	[ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveTypeResponse> GetAsync([FromRoute, Required] Guid leaveTypeId)
	{
		var leaveType = await _leaveTypeAppService.GetAsync(leaveTypeId);
		return LeaveTypeContractMapper.ToResponse(leaveType);
	}

	/// <summary>
	/// Create a new leave type
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<ActionResult<LeaveTypeResponse>> CreateAsync(
		[FromBody, Required] LeaveTypeCreateRequest request)
	{
		var createDto = LeaveTypeContractMapper.ToCreateDto(request);
		var leaveType = await _leaveTypeAppService.CreateAsync(createDto);
		var response = LeaveTypeContractMapper.ToResponse(leaveType);

		return CreatedAtAction(nameof(GetAsync), new { leaveTypeId = response.Id }, response);
	}

	/// <summary>
	/// Update an existing leave type
	/// </summary>
	[HttpPut("{leaveTypeId}")]
	[ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveTypeResponse> UpdateAsync(
		[FromRoute, Required] Guid leaveTypeId,
		[FromBody, Required] LeaveTypeUpdateRequest request)
	{
		var updateDto = LeaveTypeContractMapper.ToUpdateDto(request);
		var leaveType = await _leaveTypeAppService.UpdateAsync(leaveTypeId, updateDto);
		return LeaveTypeContractMapper.ToResponse(leaveType);
	}

	/// <summary>
	/// Delete a leave type
	/// </summary>
	[HttpDelete("{leaveTypeId}")]
	[ProducesResponseType(typeof(LeaveTypeDeleteResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveTypeDeleteResponse> DeleteAsync([FromRoute, Required] Guid leaveTypeId)
	{
		await _leaveTypeAppService.DeleteAsync(leaveTypeId);
		return LeaveTypeContractMapper.ToDeleteResponse(leaveTypeId);
	}
}
