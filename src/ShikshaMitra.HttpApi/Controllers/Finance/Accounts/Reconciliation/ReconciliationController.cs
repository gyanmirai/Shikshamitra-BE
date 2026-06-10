using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using System.ComponentModel.DataAnnotations;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Reconciliation;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/v1/finance/reconciliations")]
[Authorize]
public class ReconciliationController(
	IReconciliationAppService appService
) : AbpControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ReconciliationListResponse), StatusCodes.Status200OK)]
    public virtual async Task<ReconciliationListResponse> GetListAsync(
        [FromQuery, Required] Guid academicYearId,
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] PaymentMethodEnum? paymentMethod,
        [FromQuery] FeeReceiptVerificationStatus? status,
        [FromQuery] string? search,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var input = new GetReconciliationListInput
        {
            AcademicYearId     = academicYearId,
            Month              = month,
            Year               = year,
            PaymentMethod      = paymentMethod,
            VerificationStatus = status,
            Search             = search,
            Page               = page,
            PageSize           = pageSize,
        };
        return await appService.GetListAsync(input, cancellationToken);
    }

    [HttpGet("tab-counts")]
    [ProducesResponseType(typeof(ReconciliationTabCountsDto), StatusCodes.Status200OK)]
    public virtual async Task<ReconciliationTabCountsDto> GetTabCountsAsync(
        [FromQuery, Required] Guid academicYearId,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        return await appService.GetTabCountsAsync(academicYearId, month, year, cancellationToken);
    }

    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(ReconciliationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ReconciliationActionResponse> ApproveAsync(
        [FromRoute, Required] Guid id,
        [FromBody] ApproveReconciliationInput? body,
        CancellationToken cancellationToken = default)
    {
        return await appService.ApproveAsync(id, body ?? new ApproveReconciliationInput(), cancellationToken);
    }

    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(ReconciliationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ReconciliationActionResponse> RejectAsync(
        [FromRoute, Required] Guid id,
        [FromBody, Required] RejectReconciliationInput body,
        CancellationToken cancellationToken = default)
    {
        return await appService.RejectAsync(id, body, cancellationToken);
    }
}
