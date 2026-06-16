using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Application service interface for the fee-refund workflow.
/// HTTP exposure is handled exclusively by <c>FeeRefundController</c>.
/// </summary>
public interface IFeeRefundAppService : IApplicationService
{
    /// <summary>Paginated list of refund requests, optionally filtered.</summary>
    Task<PagedResultDto<FeeRefundRequestDto>> GetListAsync(GetFeeRefundsInput input);

    /// <summary>Single refund request with its lines.</summary>
    Task<FeeRefundRequestDto> GetAsync(Guid id);

    /// <summary>
    /// Returns the refund eligibility summary for a student + academic year.
    /// Used by the frontend to populate the fee-head refund table with real data.
    /// </summary>
    Task<RefundEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId);

    /// <summary>Creates a new refund request in Pending status.</summary>
    Task<FeeRefundRequestDto> CreateAsync(CreateFeeRefundRequestDto input);

    /// <summary>
    /// Updates an existing refund request.
    /// Only allowed while the request is in <see cref="FeeRefundStatus.Pending"/>.
    /// Fully replaces lines, payment details, and reason.
    /// </summary>
    Task<FeeRefundRequestDto> UpdateAsync(Guid id, UpdateFeeRefundRequestDto input);

    /// <summary>
    /// Deletes (soft-deletes) a refund request.
    /// Only allowed while the request is in <see cref="FeeRefundStatus.Pending"/>.
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>Transitions a refund request status (Approve / Reject / Cancel / UnderReview).</summary>
    Task<FeeRefundRequestDto> UpdateStatusAsync(Guid id, UpdateFeeRefundStatusDto input);
}
