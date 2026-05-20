using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Finance.FeeRefund.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.FeeRefund;

/// <summary>
/// REST endpoints for the fee-refund workflow.
/// All business logic is delegated to <see cref="IFeeRefundAppService"/>.
/// </summary>
[Authorize]
[ApiController]
[Area("finance")]
[Route("api/fee-refunds")]
public class FeeRefundController(
    IFeeRefundAppService feeRefundAppService
) : AbpControllerBase
{
    /// <summary>Returns a paged list of fee-refund requests, optionally filtered.</summary>
    /// <response code="200">List retrieved successfully.</response>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(PagedResultDto<FeeRefundRequestDto>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeRefundRequestDto>> GetListAsync([FromQuery] GetFeeRefundsInput input)
        => feeRefundAppService.GetListAsync(input);

    /// <summary>Returns a single fee-refund request with its lines.</summary>
    /// <response code="200">Request retrieved successfully.</response>
    /// <response code="404">Request not found.</response>
    [HttpGet("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
    public Task<FeeRefundRequestDto> GetAsync([FromRoute] Guid id)
        => feeRefundAppService.GetAsync(id);

    /// <summary>
    /// Returns the refund eligibility summary for a student in an academic year.
    /// Use this to populate the refund form's fee-head table with real DueFee IDs
    /// and accurate refundable balances.
    /// </summary>
    /// <response code="200">Eligibility summary retrieved successfully.</response>
    [HttpGet("eligibility")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(RefundEligibilityDto), StatusCodes.Status200OK)]
    public Task<RefundEligibilityDto> GetEligibilityAsync(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId)
        => feeRefundAppService.GetEligibilityAsync(studentId, academicYearId);

    /// <summary>Creates a new fee-refund request in Pending status.</summary>
    /// <response code="201">Refund request created successfully.</response>
    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateFeeRefundRequestDto input)
    {
        var result = await feeRefundAppService.CreateAsync(input);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    /// <summary>
    /// Transitions a refund request to a new status.
    /// Allowed transitions: Pending → UnderReview → Approved / Rejected;
    /// Pending or UnderReview → Cancelled.
    /// Approving also posts <c>DueFee.ApplyRefund</c> entries inside a transaction.
    /// </summary>
    /// <response code="200">Status updated successfully.</response>
    /// <response code="404">Request not found.</response>
    [HttpPatch("{id:guid}/status")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
    public Task<FeeRefundRequestDto> UpdateStatusAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateFeeRefundStatusDto input)
        => feeRefundAppService.UpdateStatusAsync(id, input);
}
