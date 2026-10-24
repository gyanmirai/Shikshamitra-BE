using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Receipts;

/// <summary>
/// REST endpoints for querying and downloading fee/charge receipts.
/// </summary>
[Authorize]
[ApiController]
[Area("finance")]
[Route("api/v1/fee-receipts")]
public class FeeReceiptController(
    IFeeReceiptAppService feeReceiptAppService
) : AbpControllerBase
{
    /// <summary>
    /// Returns a paginated, sorted list of receipts.
    /// Board + Medium are required; Month and ReceiptKind are optional filters.
    /// Results are ordered CreationTime DESC (newest first).
    /// </summary>
    /// <response code="200">List retrieved successfully.</response>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)]
    [ProducesResponseType(typeof(PagedResultDto<FeeReceiptListItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeReceiptListItemDto>> GetListAsync(
        [FromQuery] GetFeeReceiptListInput input,
        CancellationToken cancellationToken = default)
        => feeReceiptAppService.GetListAsync(input, cancellationToken);

    /// <summary>
    /// Returns the full detail of a single receipt, including sibling correlation
    /// and kachi ↔ final linkage.
    /// </summary>
    /// <response code="200">Receipt retrieved successfully.</response>
    /// <response code="404">Receipt not found.</response>
    [HttpGet("{id:guid}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)]
    [ProducesResponseType(typeof(FeeReceiptDetailDto), StatusCodes.Status200OK)]
    public Task<FeeReceiptDetailDto> GetAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
        => feeReceiptAppService.GetAsync(id, cancellationToken);

    /// <summary>
    /// Returns the PDF document for the given receipt.
    /// The PDF is lazily generated on first request using the receipt-kind-specific
    /// template, then stored in ABP Blob Storing for subsequent downloads.
    /// </summary>
    /// <response code="200">PDF bytes returned as application/pdf.</response>
    /// <response code="404">Receipt not found.</response>
    [HttpGet("{id:guid}/pdf")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPdfAsync(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var pdfBytes = await feeReceiptAppService.GetPdfAsync(id, cancellationToken);
        var receipt = await feeReceiptAppService.GetAsync(id, cancellationToken);
        return File(pdfBytes, MediaTypeNames.Application.Pdf, receipt.PdfFileName);
    }
}
