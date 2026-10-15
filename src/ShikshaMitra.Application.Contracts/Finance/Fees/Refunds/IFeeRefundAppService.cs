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
    /// Returns pinned refund eligibility for a Student or admission Finance account.
    /// An original receipt limits the result to its settled source allocations.
    /// </summary>
    Task<RefundEligibilityDto> GetEligibilityAsync(Guid? studentId, Guid academicYearId,
        Guid? financeFeeAccountId = null, Guid? originalReceiptId = null);

    Task<FeeRefundPreviewDto> PreviewAsync(CreateFeeRefundRequestDto input);

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
    Task<FeeRefundRequestDto> SubmitAsync(Guid id);
    Task<FeeRefundRequestDto> StartReviewAsync(Guid id);
    Task<FeeRefundRequestDto> ApproveAsync(Guid id, FeeRefundActionDto input);
    Task<FeeRefundRequestDto> RejectAsync(Guid id, RejectFeeRefundDto input);
    Task<FeeRefundRequestDto> CancelAsync(Guid id, FeeRefundActionDto input);
    Task<FeeRefundRequestDto> ProcessAsync(Guid id, ProcessFeeRefundDto input);
    Task<FeeRefundRequestDto> RecordPayoutAsync(Guid id, RecordRefundPayoutDto input);
    Task<FeeRefundRequestDto> ConfirmPayoutAsync(Guid id, Guid payoutId, ResolveRefundPayoutDto input);
    Task<FeeRefundRequestDto> FailPayoutAsync(Guid id, Guid payoutId, ResolveRefundPayoutDto input);
    Task<FeeRefundRequestDto> ReturnPayoutAsync(Guid id, ReturnRefundPayoutDto input);
    Task<FeeRefundRequestDto> ReverseAsync(Guid id, ReverseFeeRefundDto input);
}
