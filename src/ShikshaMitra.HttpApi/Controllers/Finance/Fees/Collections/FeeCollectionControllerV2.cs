using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Collections;

/// <summary>
/// V2 fee-collection controller.
/// </summary>
/// <remarks>
/// <para><b>What is new in V2 vs V1:</b></para>
/// <list type="bullet">
///   <item>All filtering, sorting, and pagination are executed on the database — no in-memory list hydration.</item>
///   <item>Support for <c>StandardId</c> and <c>DivisionId</c> query filters.</item>
///   <item>Status filter is pushed to the SQL query.</item>
///   <item>ABP standard <c>SkipCount</c> / <c>MaxResultCount</c> pagination.</item>
///   <item>Export endpoint returns CSV or Excel byte streams.</item>
/// </list>
/// </remarks>
[Authorize]
[ApiController]
[Area("finance")]
[Route("api/v2/fee-collections")]
public class FeeCollectionControllerV2(
    IFeeCollectionAppServiceV2 feeCollectionAppServiceV2
) : AbpControllerBase
{
    /// <summary>
    /// Returns a server-side filtered, sorted, and paginated summary of
    /// student-wise fee collection for the given academic context.
    /// </summary>
    /// <remarks>
    /// <b>Filters:</b> AcademicYearId, BoardId, EducationMediumId (all required),
    /// StandardId, DivisionId, Search (≥ 2 chars), Status (0=Pending / 1=Paid / 2=Overdue).<br/>
    /// <b>Sorting:</b> Pass <c>sorting</c> as e.g. <c>"pendingFees desc"</c>.<br/>
    /// <b>Pagination:</b> ABP standard <c>skipCount</c> + <c>maxResultCount</c> (max 100).
    /// </remarks>
    /// <response code="200">Paginated fee-collection summary returned successfully.</response>
    [HttpGet]
    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    [ProducesResponseType(typeof(PagedResultDto<FeeCollectionSummaryItemDtoV2>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
        [FromQuery] GetFeeCollectionSummaryInputV2 input,
        CancellationToken cancellationToken)
        => feeCollectionAppServiceV2.GetSummaryAsync(input, cancellationToken);

    /// <summary>
    /// Exports the full (unpaginated) fee-collection summary as a CSV or Excel file.
    /// </summary>
    /// <remarks>
    /// Accepts the same filters as <see cref="GetSummaryAsync"/> minus pagination.<br/>
    /// Set <c>format=0</c> for CSV (default) or <c>format=1</c> for Excel (.xlsx).
    /// </remarks>
    /// <response code="200">File stream returned with appropriate <c>Content-Disposition</c> header.</response>
    [HttpGet("export")]
    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] FeeCollectionExportRequestDto input,
        CancellationToken cancellationToken)
    {
        var bytes = await feeCollectionAppServiceV2.ExportAsync(input, cancellationToken);

        var isExcel   = input.Format == FeeCollectionExportFormat.Excel;
        var mimeType  = isExcel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var extension = isExcel ? "xlsx" : "csv";
        var fileName  = $"fee-collection-{DateTime.UtcNow:yyyyMMdd}.{extension}";

        return File(bytes, mimeType, fileName);
    }
}
