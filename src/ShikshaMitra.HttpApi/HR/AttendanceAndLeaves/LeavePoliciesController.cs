using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.AttendanceAndLeaves.LeavePolicyInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

/// <summary>
/// Leave policy management controller
/// Handles CRUD operations for employee leave policies in the school ERP system
/// </summary>
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

	/// <summary>
	/// Get paginated list of leave policies
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(LeavePolicyListResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeavePolicyListResponse> GetListAsync(
		[FromQuery, Required] Guid mediumStageId,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(10, 100)] int pageSize = 10)
	{
		var input = new PagedAndSortedResultRequestDto
		{
			SkipCount = (page - 1) * pageSize,
			MaxResultCount = pageSize
		};

		var result = await _leavePolicyAppService.GetListAsync(input);
		return LeavePolicyContractMapper.ToListResponse(result, page, pageSize);
	}

	/// <summary>
	/// Create a new leave policy
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(LeavePolicyResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<ActionResult<LeavePolicyResponse>> CreateAsync(
		[FromBody, Required] LeavePolicyCreateRequest request)
	{
		var createDto = LeavePolicyContractMapper.ToCreateDto(request);
		var leavePolicy = await _leavePolicyAppService.CreateAsync(createDto);
		var response = LeavePolicyContractMapper.ToResponse(leavePolicy);

		return StatusCode(StatusCodes.Status201Created, response);
	}

	/// <summary>
	/// Update an existing leave policy
	/// </summary>
	[HttpPut("{policyId}")]
	[ProducesResponseType(typeof(LeavePolicyUpdatedResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<LeavePolicyUpdatedResponse> UpdateAsync(
		[FromRoute, Required] string policyId,
		[FromBody, Required] LeavePolicyCreateRequest request)
	{
		if (!Guid.TryParse(policyId, out var id))
		{
			throw new ArgumentException("Invalid policy ID format", nameof(policyId));
		}

		var updateDto = LeavePolicyContractMapper.ToUpdateDto(request);
		var leavePolicy = await _leavePolicyAppService.UpdateAsync(id, updateDto);
		return LeavePolicyContractMapper.ToUpdatedResponse(leavePolicy);
	}

	/// <summary>
	/// Delete a leave policy (soft delete)
	/// </summary>
	[HttpDelete("{policyId}")]
	[ProducesResponseType(typeof(LeavePolicyDeleteResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status500InternalServerError)]
	public virtual async Task<LeavePolicyDeleteResponse> DeleteAsync([FromRoute, Required] string policyId)
	{
		if (!Guid.TryParse(policyId, out var id))
		{
			throw new ArgumentException("Invalid policy ID format", nameof(policyId));
		}

		await _leavePolicyAppService.DeleteAsync(id);
		return LeavePolicyContractMapper.ToDeleteResponse(id);
	}
}
