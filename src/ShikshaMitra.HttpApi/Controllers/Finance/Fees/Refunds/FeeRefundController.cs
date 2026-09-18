using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.FeeRefund;

[Authorize]
[ApiController]
[Area("finance")]
[Route("api/fee-refunds")]
public class FeeRefundController(
    IFeeRefundAppService feeRefundAppService
) : AbpControllerBase
{
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(PagedResultDto<FeeRefundRequestDto>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeRefundRequestDto>> GetListAsync([FromQuery] GetFeeRefundsInput input)
        => feeRefundAppService.GetListAsync(input);

    [HttpGet("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
    public Task<FeeRefundRequestDto> GetAsync([FromRoute] Guid id)
        => feeRefundAppService.GetAsync(id);

    [HttpGet("eligibility")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(RefundEligibilityDto), StatusCodes.Status200OK)]
    public Task<RefundEligibilityDto> GetEligibilityAsync(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId)
        => feeRefundAppService.GetEligibilityAsync(studentId, academicYearId);

    [HttpPost("preview")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public Task<FeeRefundPreviewDto> PreviewAsync([FromBody] CreateFeeRefundRequestDto input)
        => feeRefundAppService.PreviewAsync(input);

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateFeeRefundRequestDto input)
    {
        var result = await feeRefundAppService.CreateAsync(input);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Edit)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
    public Task<FeeRefundRequestDto> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateFeeRefundRequestDto input)
        => feeRefundAppService.UpdateAsync(id, input);

    [HttpDelete("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync([FromRoute] Guid id)
    {
        await feeRefundAppService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
    public Task<FeeRefundRequestDto> UpdateStatusAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateFeeRefundStatusDto input)
        => feeRefundAppService.UpdateStatusAsync(id, input);

    [HttpPost("{id:guid}/submit")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Submit)]
    public Task<FeeRefundRequestDto> SubmitAsync(Guid id) => feeRefundAppService.SubmitAsync(id);

    [HttpPost("{id:guid}/start-review")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.StartReview)]
    public Task<FeeRefundRequestDto> StartReviewAsync(Guid id) => feeRefundAppService.StartReviewAsync(id);

    [HttpPost("{id:guid}/approve")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
    public Task<FeeRefundRequestDto> ApproveAsync(Guid id, [FromBody] FeeRefundActionDto input) => feeRefundAppService.ApproveAsync(id, input);

    [HttpPost("{id:guid}/reject")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reject)]
    public Task<FeeRefundRequestDto> RejectAsync(Guid id, [FromBody] RejectFeeRefundDto input) => feeRefundAppService.RejectAsync(id, input);

    [HttpPost("{id:guid}/cancel")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Cancel)]
    public Task<FeeRefundRequestDto> CancelAsync(Guid id, [FromBody] FeeRefundActionDto input) => feeRefundAppService.CancelAsync(id, input);

    [HttpPost("{id:guid}/process")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public Task<FeeRefundRequestDto> ProcessAsync(Guid id, [FromBody] ProcessFeeRefundDto input) => feeRefundAppService.ProcessAsync(id, input);

    [HttpPost("{id:guid}/reverse")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Reverse)]
    public Task<FeeRefundRequestDto> ReverseAsync(Guid id, [FromBody] ReverseFeeRefundDto input) => feeRefundAppService.ReverseAsync(id, input);
}
