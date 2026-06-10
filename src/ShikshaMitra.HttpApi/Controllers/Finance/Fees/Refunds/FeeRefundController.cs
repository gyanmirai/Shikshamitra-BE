using System;
using System.Threading.Tasks;
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

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateFeeRefundRequestDto input)
    {
        var result = await feeRefundAppService.CreateAsync(input);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
    [ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
    public Task<FeeRefundRequestDto> UpdateStatusAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateFeeRefundStatusDto input)
        => feeRefundAppService.UpdateStatusAsync(id, input);
}
