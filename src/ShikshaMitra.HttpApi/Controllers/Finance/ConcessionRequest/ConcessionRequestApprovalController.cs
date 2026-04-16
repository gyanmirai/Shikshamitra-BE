using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.Finance.ConcessionRequest;

/// <summary>
/// Manages the approval lifecycle: approve, reject, admin application listing,
/// and deletion of non-approved applications.
/// </summary>
[Authorize]
[Route("api/concession-requests")]
public class ConcessionRequestApprovalController : AbpController
{
	private readonly IConcessionRequestApprovalAppService _appService;

	public ConcessionRequestApprovalController(IConcessionRequestApprovalAppService appService)
	{
		_appService = appService;
	}

	/// <summary>
	/// Approves a Pending concession request with an optional approver message.
	/// Returns the approval details including approver identity and timestamp.
	/// </summary>
	[HttpPost("{studentConcessionTypeRequestId:guid}/approve")]
	[ProducesResponseType(typeof(ConcessionApprovalResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<ConcessionApprovalResponseDto> ApproveAsync(
		Guid studentConcessionTypeRequestId,
		[FromBody] ApproveConcessionRequestDto input)
		=> _appService.ApproveAsync(studentConcessionTypeRequestId, input);

	/// <summary>
	/// Rejects a Pending concession request with a mandatory rejection reason.
	/// Returns the rejection details including rejector identity and timestamp.
	/// </summary>
	[HttpPost("{studentConcessionTypeRequestId:guid}/reject")]
	[ProducesResponseType(typeof(ConcessionRejectionResponseDto), 200)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<ConcessionRejectionResponseDto> RejectAsync(
		Guid studentConcessionTypeRequestId,
		[FromBody] RejectConcessionRequestDto input)
		=> _appService.RejectAsync(studentConcessionTypeRequestId, input);

	/// <summary>
	/// Returns a paginated list of concession applications scoped to a board and education
	/// medium, ordered newest-first. Includes per-status counts (Pending, Approved, Rejected)
	/// computed independently of the active status filter. Defaults to Draft when no status
	/// is specified.
	/// </summary>
	[HttpGet("~/api/concession-request/concession-applications")]
	[ProducesResponseType(typeof(ConcessionApplicationsDto), 200)]
	[ProducesResponseType(401)]
	public virtual Task<ConcessionApplicationsDto> GetConcessionApplicationsAsync(
		[FromQuery] GetConcessionApplicationsInput input)
		=> _appService.GetConcessionApplicationsAsync(input);

	/// <summary>
	/// Deletes a Pending or Rejected concession application.
	/// Approved applications cannot be deleted. Idempotent — returns 204 even when
	/// the application has already been deleted.
	/// </summary>
	[HttpDelete("~/api/concession-request/delete-concession/{studentConcessionTypeRequestId:guid}")]
	[ProducesResponseType(204)]
	[ProducesResponseType(400)]
	[ProducesResponseType(401)]
	public virtual async Task<IActionResult> DeleteConcessionApplicationAsync(
		Guid studentConcessionTypeRequestId)
	{
		await _appService.DeleteConcessionApplicationAsync(studentConcessionTypeRequestId);
		return NoContent();
	}
}
