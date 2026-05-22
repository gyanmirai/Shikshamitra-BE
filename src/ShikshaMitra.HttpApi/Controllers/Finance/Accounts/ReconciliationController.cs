using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Finance.Accounts.Reconciliation.Dtos;
using ShikshaMitra.Finance.Receipts;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Finance.Accounts;

/// <summary>
/// HTTP API for the Finance Reconciliation inbox.
/// All state-mutating actions use PATCH (HR Leave pattern).
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/v1/finance/reconciliations")]
[Authorize]
public class ReconciliationController : AbpControllerBase
{
    private readonly IReconciliationAppService _appService;

    public ReconciliationController(IReconciliationAppService appService)
    {
        _appService = appService;
    }

    /// <summary>
    /// Returns the paged reconciliation inbox, plus badge counts for all 7 payment tabs.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ReconciliationListResponse), StatusCodes.Status200OK)]
    public virtual async Task<ReconciliationListResponse> GetListAsync(
        [FromQuery, Required] Guid academicYearId,
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] string? paymentMethod,
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
        return await _appService.GetListAsync(input, cancellationToken);
    }

    /// <summary>
    /// Returns only the badge counts for all 7 payment-method tabs.
    /// Lightweight endpoint for refreshing counts without re-fetching list rows.
    /// </summary>
    [HttpGet("tab-counts")]
    [ProducesResponseType(typeof(ReconciliationTabCountsDto), StatusCodes.Status200OK)]
    public virtual async Task<ReconciliationTabCountsDto> GetTabCountsAsync(
        [FromQuery, Required] Guid academicYearId,
        [FromQuery] int? month,
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        return await _appService.GetTabCountsAsync(academicYearId, month, year, cancellationToken);
    }

    /// <summary>
    /// Approves a pending provisional fee receipt.
    /// Creates a final FIN-* receipt and marks the provisional as Verified.
    /// </summary>
    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(ReconciliationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ReconciliationActionResponse> ApproveAsync(
        [FromRoute, Required] Guid id,
        [FromBody] ApproveReconciliationInput? body,
        CancellationToken cancellationToken = default)
    {
        return await _appService.ApproveAsync(id, body ?? new ApproveReconciliationInput(), cancellationToken);
    }

    /// <summary>
    /// Rejects a pending provisional fee receipt with mandatory remarks.
    /// Reverses all DueFee allocations and marks the collection transaction as Reversed.
    /// </summary>
    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(ReconciliationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ReconciliationActionResponse> RejectAsync(
        [FromRoute, Required] Guid id,
        [FromBody, Required] RejectReconciliationInput body,
        CancellationToken cancellationToken = default)
    {
        return await _appService.RejectAsync(id, body, cancellationToken);
    }
}
