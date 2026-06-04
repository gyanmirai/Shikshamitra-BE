using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Collections;

[Authorize]
[ApiController]
[Area("finance")]
[Route("api/v2/fee-collections")]
public class FeeCollectionController(
    IFeeCollectionAppService feeCollectionAppService
) : AbpControllerBase
{
    [HttpGet]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(PagedResultDto<FeeCollectionSummaryItemDto>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeCollectionSummaryItemDto>> GetSummaryAsync(
        [FromQuery] GetFeeCollectionSummaryInput input,
        CancellationToken cancellationToken)
        => feeCollectionAppService.GetSummaryAsync(input, cancellationToken);

    [HttpGet("export")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAsync(
        [FromQuery] FeeCollectionExportRequestDto input,
        CancellationToken cancellationToken)
    {
        var bytes = await feeCollectionAppService.ExportAsync(input, cancellationToken);

        var isExcel   = input.Format == FeeCollectionExportFormat.Excel;
        var mimeType  = isExcel
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var extension = isExcel ? "xlsx" : "csv";
        var fileName  = $"fee-collection-{DateTime.UtcNow:yyyyMMdd}.{extension}";

        return File(bytes, mimeType, fileName);
    }

    [HttpPost]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
    [ProducesResponseType(typeof(CollectFeeOutput), StatusCodes.Status201Created)]
    public async Task<IActionResult> CollectFeeAsync([FromBody] CollectFeeInput input)
    {
        var result = await feeCollectionAppService.CollectFeeAsync(input);
        return CreatedAtAction(
            actionName: nameof(GetStudentFeeDetailsAsync),
            routeValues: new { studentId = result.StudentId },
            value: result);
    }

    [HttpGet("{studentId:guid}/collect-form")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(FeeCollectFormDto), StatusCodes.Status200OK)]
    public Task<FeeCollectFormDto> GetCollectFormAsync(
        [FromRoute] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
        => feeCollectionAppService.GetCollectFormAsync(studentId, academicYearId, cancellationToken);

    [HttpGet("{studentId:guid}/fee-details")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    [ProducesResponseType(typeof(StudentFeeDetailsDto), StatusCodes.Status200OK)]
    public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(
        [FromRoute] Guid studentId,
        [FromQuery] Guid academicYearId,
        CancellationToken cancellationToken)
        => feeCollectionAppService.GetStudentFeeDetailsAsync(studentId, academicYearId, cancellationToken);
}
