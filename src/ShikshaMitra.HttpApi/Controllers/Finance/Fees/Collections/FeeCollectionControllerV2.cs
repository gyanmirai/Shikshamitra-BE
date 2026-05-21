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
/// V2 fee collection controller — serves all fee collection operations.
/// All endpoints are scoped to <c>api/v2/fee-collections</c>.
/// </summary>
[Authorize]
[ApiController]
[Area("finance")]
[Route("api/v2/fee-collections")]
public class FeeCollectionControllerV2(
    IFeeCollectionAppServiceV2 feeCollectionAppServiceV2
) : AbpControllerBase
{
    /// <summary>
    /// Returns a server-side paginated, filtered, and sorted summary of students
    /// with their fee collection status for the given academic context.
    /// </summary>
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(PagedResultDto<FeeCollectionSummaryItemDtoV2>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
        [FromQuery] GetFeeCollectionSummaryInputV2 input,
        CancellationToken cancellationToken)
        => feeCollectionAppServiceV2.GetSummaryAsync(input, cancellationToken);

    /// <summary>
    /// Exports the fee-collection summary as a CSV or Excel file.
    /// </summary>
    [HttpGet("export")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
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

    /// <summary>
    /// Records a fee payment for a student with support for multiple payment methods,
    /// fee heads, and optional per-month DueFee allocations.
    /// </summary>
    /// <response code="201">Fee collected successfully.</response>
    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
    [ProducesResponseType(typeof(CollectFeeOutput), StatusCodes.Status201Created)]
    public async Task<IActionResult> CollectFeeAsync([FromBody] CollectFeeInput input)
    {
        var result = await feeCollectionAppServiceV2.CollectFeeAsync(input);
        return CreatedAtAction(
            actionName: nameof(GetStudentFeeDetailsAsync),
            routeValues: new { studentId = result.StudentId },
            value: result);
    }

    /// <summary>
    /// Returns the data needed to populate the collect-fee dialog:
    /// student context, month-wise DueFee ledger, and approved charges.
    /// </summary>
    /// <response code="200">Collect form data retrieved successfully.</response>
    [HttpGet("{studentId:guid}/collect-form")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(FeeCollectFormDto), StatusCodes.Status200OK)]
    public Task<FeeCollectFormDto> GetCollectFormAsync(
        [FromRoute] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
        => feeCollectionAppServiceV2.GetCollectFormAsync(studentId, academicYearId, cancellationToken);

    /// <summary>
    /// Returns complete fee information for a student in an academic year:
    /// fee-head breakdown, payment history, concessions, and overview totals.
    /// </summary>
    /// <response code="200">Fee details retrieved successfully.</response>
    [HttpGet("{studentId:guid}/fee-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(StudentFeeDetailsDto), StatusCodes.Status200OK)]
    public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
        [FromRoute] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
        => feeCollectionAppServiceV2.GetStudentFeeDetailsAsync(studentId, academicYearId, cancellationToken);
}
