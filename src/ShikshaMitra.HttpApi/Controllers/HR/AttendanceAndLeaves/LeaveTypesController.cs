using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.AttendanceAndLeaves;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr-leave-types")]
[Authorize]
public class LeaveTypesController : AbpControllerBase
{
	private readonly ILeaveTypeAppService _leaveTypeAppService;

	public LeaveTypesController(ILeaveTypeAppService leaveTypeAppService)
	{
		_leaveTypeAppService = leaveTypeAppService;
	}

	[HttpGet]
	[ProducesResponseType(typeof(LeaveTypeListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<LeaveTypeListResponse> GetListAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 10,
		[FromQuery] string? search = null,
		CancellationToken cancellationToken = default)
	{
		return await _leaveTypeAppService.GetListByAcademicYearIdAsync(
			academicYearId, page, pageSize, search, cancellationToken);
	}

	[HttpGet("{id}")]
	[ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveTypeResponse> GetAsync([FromRoute, Required] Guid id)
	{
		return await _leaveTypeAppService.GetLeaveTypeByIdAsync(id);
	}

	[HttpPost]
	[ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<ActionResult<LeaveTypeResponse>> CreateAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromBody, Required] LeaveTypeCreateRequest request)
	{
		var response = await _leaveTypeAppService.CreateLeaveTypeAsync(academicYearId, request);
		return CreatedAtAction(nameof(GetAsync), new { id = response.Id }, response);
	}

	[HttpPut("{id}")]
	[ProducesResponseType(typeof(LeaveTypeResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveTypeResponse> UpdateAsync(
		[FromRoute, Required] Guid id,
		[FromBody, Required] LeaveTypeUpdateRequest request)
	{
		return await _leaveTypeAppService.UpdateLeaveTypeAsync(id, request);
	}

	[HttpDelete("{id}")]
	[ProducesResponseType(typeof(LeaveTypeDeleteResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveTypeDeleteResponse> DeleteAsync([FromRoute, Required] Guid id)
	{
		return await _leaveTypeAppService.DeleteLeaveTypeAsync(id);
	}
}
