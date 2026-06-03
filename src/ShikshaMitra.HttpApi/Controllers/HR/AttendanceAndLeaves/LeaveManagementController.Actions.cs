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

	/// <summary>
	/// Cancel own leave request.
	/// </summary>
	[HttpDelete("my/{id:guid}")]
	[ProducesResponseType(typeof(LeaveRequestDeleteResponse), StatusCodes.Status200OK)]
	public virtual async Task<LeaveRequestDeleteResponse> DeleteMyAsync([FromRoute, Required] Guid id)
	{
		var deleted = await _leaveManagementAppService.DeleteMyAsync(id);
		return LeaveManagementContractMapper.ToDeleteResponse(deleted);
	}
}
