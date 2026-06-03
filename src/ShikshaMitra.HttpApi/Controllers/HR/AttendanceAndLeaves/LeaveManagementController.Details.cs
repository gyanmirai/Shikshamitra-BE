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
	/// Get own leave request by ID.
	/// </summary>
	[HttpGet("my/{id:guid}")]
	[ProducesResponseType(typeof(LeaveRequestDetailsResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<LeaveRequestDetailsResponse> GetMyAsync([FromRoute, Required] Guid id)
	{
		var details = await _leaveManagementAppService.GetMyAsync(id);
		return LeaveManagementContractMapper.ToDetailsResponse(details);
	}
}
