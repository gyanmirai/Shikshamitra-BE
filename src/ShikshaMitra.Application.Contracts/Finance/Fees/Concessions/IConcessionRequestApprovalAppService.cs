using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;

namespace ShikshaMitra.Finance.Fees.Concessions;

/// <summary>
/// Manages the approval lifecycle of concession applications:
/// approve, reject, admin listing, and deletion of non-approved applications.
/// </summary>
public interface IConcessionRequestApprovalAppService : IApplicationService
{
	/// <summary>
	/// Approves a Pending concession request with an optional approver message.
	/// Returns the approval details including approver identity and timestamp.
	/// </summary>
	Task<ConcessionApprovalResponseDto> ApproveAsync(
		Guid studentConcessionTypeRequestId,
		ApproveConcessionRequestDto input);

	/// <summary>
	/// Rejects a Pending concession request with a mandatory rejection reason.
	/// Returns the rejection details including rejector identity and timestamp.
	/// </summary>
	Task<ConcessionRejectionResponseDto> RejectAsync(
		Guid studentConcessionTypeRequestId,
		RejectConcessionRequestDto input);

	/// <summary>
	/// Returns a paginated list of concession applications scoped to a board and
	/// education medium, ordered newest-first. Includes per-status counts (Pending,
	/// Approved, Rejected) computed independently of the active status filter.
	/// Defaults to Draft applications when no status is specified.
	/// </summary>
	Task<ConcessionApplicationsDto> GetConcessionApplicationsAsync(GetConcessionApplicationsInput input);

	/// <summary>
	/// Cancels a Draft or Pending concession request (→ Cancelled).
	/// Returns cancellation details. Approved/Rejected/Cancelled requests cannot be cancelled.
	/// </summary>
	Task<ConcessionCancellationResponseDto> CancelAsync(Guid studentConcessionTypeRequestId);

	/// <summary>
	/// Deletes a Pending or Rejected concession application. Approved applications
	/// cannot be deleted. Idempotent — succeeds silently when already deleted.
	/// </summary>
	Task DeleteConcessionApplicationAsync(Guid studentConcessionTypeRequestId);
}
