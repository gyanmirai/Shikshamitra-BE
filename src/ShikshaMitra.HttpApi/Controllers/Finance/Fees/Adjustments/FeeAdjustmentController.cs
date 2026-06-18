using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.FeeAdjustment;

[Authorize]
[ApiController]
[Area("finance")]
[Route("api/fee-adjustments")]
public class FeeAdjustmentController(IFeeAdjustmentAppService feeAdjustmentAppService) : AbpControllerBase
{
    [HttpGet("eligibility")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    [ProducesResponseType(typeof(AdjustmentEligibilityDto), StatusCodes.Status200OK)]
    public Task<AdjustmentEligibilityDto> GetEligibilityAsync(
        [FromQuery] Guid studentId,
        [FromQuery] Guid academicYearId) =>
        feeAdjustmentAppService.GetEligibilityAsync(studentId, academicYearId);

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [ProducesResponseType(typeof(FeeAdjustmentDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromQuery] Guid academicYearId,
        [FromBody] CreateFeeAdjustmentDto input)
    {
        var result = await feeAdjustmentAppService.CreateAsync(academicYearId, input);
        return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [ProducesResponseType(typeof(FeeAdjustmentDetailDto), StatusCodes.Status200OK)]
    public Task<FeeAdjustmentDetailDto> UpdateAsync(
        Guid id,
        [FromQuery] Guid academicYearId,
        [FromBody] UpdateFeeAdjustmentDto input) =>
        feeAdjustmentAppService.UpdateAsync(id, academicYearId, input);

    [HttpDelete("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await feeAdjustmentAppService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    [ProducesResponseType(typeof(FeeAdjustmentDetailDto), StatusCodes.Status200OK)]
    public Task<FeeAdjustmentDetailDto> GetAsync(Guid id) =>
        feeAdjustmentAppService.GetAsync(id);

    [HttpGet("applications")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    [ProducesResponseType(typeof(FeeAdjustmentApplicationsDto), StatusCodes.Status200OK)]
    public Task<FeeAdjustmentApplicationsDto> GetApplicationsAsync([FromQuery] GetFeeAdjustmentApplicationsInput input) =>
        feeAdjustmentAppService.GetApplicationsAsync(input);

    [HttpGet("{id:guid}/apply-context")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [ProducesResponseType(typeof(FeeAdjustmentApplyContextDto), StatusCodes.Status200OK)]
    public Task<FeeAdjustmentApplyContextDto> GetApplyContextAsync(Guid id) =>
        feeAdjustmentAppService.GetApplyContextAsync(id);

    [HttpPost("{id:guid}/apply")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [ProducesResponseType(typeof(ApplyFeeAdjustmentResultDto), StatusCodes.Status200OK)]
    public Task<ApplyFeeAdjustmentResultDto> ApplyAsync(
        Guid id,
        [FromBody] ApplyFeeAdjustmentDto input,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey) =>
        feeAdjustmentAppService.ApplyAsync(id, input, idempotencyKey);

    [HttpPost("{id:guid}/reject")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    [ProducesResponseType(typeof(RejectFeeAdjustmentResultDto), StatusCodes.Status200OK)]
    public Task<RejectFeeAdjustmentResultDto> RejectAsync(Guid id, [FromBody] RejectFeeAdjustmentDto input) =>
        feeAdjustmentAppService.RejectAsync(id, input);
}
