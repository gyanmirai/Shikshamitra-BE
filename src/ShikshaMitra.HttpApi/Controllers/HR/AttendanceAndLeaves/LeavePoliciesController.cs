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
[Route("api/leave-policies")]
[Authorize]
public class LeavePoliciesController : AbpControllerBase
{
	private readonly ILeavePolicyAppService _leavePolicyAppService;

	public LeavePoliciesController(ILeavePolicyAppService leavePolicyAppService)
	{
		_leavePolicyAppService = leavePolicyAppService;
	}

	[HttpGet]
	[ProducesResponseType(typeof(LeavePolicyListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<LeavePolicyListResponse> GetListAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 500)] int pageSize = 10,
		[FromQuery] string? search = null,
		CancellationToken cancellationToken = default)
	{
		return await _leavePolicyAppService.GetListByAcademicYearIdAsync(
			academicYearId, page, pageSize, search, cancellationToken);
	}

	[HttpGet("{id}")]
	[ProducesResponseType(typeof(LeavePolicyDetailResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeavePolicyDetailResponse> GetAsync([FromRoute, Required] Guid id)
	{
		return await _leavePolicyAppService.GetLeavePolicyByIdAsync(id);
	}

	[HttpPost]
	[ProducesResponseType(typeof(LeavePolicyResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public virtual async Task<ActionResult<LeavePolicyResponse>> CreateAsync(
		[FromQuery, Required] Guid academicYearId,
		[FromBody, Required] LeavePolicyCreateRequest request)
	{
		var response = await _leavePolicyAppService.CreateLeavePolicyAsync(academicYearId, request);
		return StatusCode(StatusCodes.Status201Created, response);
	}

	[HttpPut("{id}")]
	[ProducesResponseType(typeof(LeavePolicyDetailResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeavePolicyDetailResponse> UpdateAsync(
		[FromRoute, Required] Guid id,
		[FromBody, Required] LeavePolicyUpdateRequest request)
	{
		return await _leavePolicyAppService.UpdateLeavePolicyAsync(id, request);
	}

	[HttpDelete("{id}")]
	[ProducesResponseType(typeof(LeavePolicyDeleteResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public virtual async Task<LeavePolicyDeleteResponse> DeleteAsync([FromRoute, Required] Guid id)
	{
		return await _leavePolicyAppService.DeleteLeavePolicyAsync(id);
	}
}
