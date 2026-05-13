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

[Authorize]
[ApiController]
[Area("finance")]
[Route("api/v2/fee-collections")]
public class FeeCollectionControllerV2(
    IFeeCollectionAppServiceV2 feeCollectionAppServiceV2
) : AbpControllerBase
{
    [HttpGet]
    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    [ProducesResponseType(typeof(PagedResultDto<FeeCollectionSummaryItemDtoV2>), StatusCodes.Status200OK)]
    public Task<PagedResultDto<FeeCollectionSummaryItemDtoV2>> GetSummaryAsync(
        [FromQuery] GetFeeCollectionSummaryInputV2 input,
        CancellationToken cancellationToken)
        => feeCollectionAppServiceV2.GetSummaryAsync(input, cancellationToken);

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
