using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Dashboard;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Finance.Dashboard;

[ApiController, Area("app"), RemoteService(Name = "Default"), Route("api/finance/dashboard")]
[Authorize(ShikshaMitraSubModulePermissions.FinanceDashboard)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FinanceDashboardController(IFinanceDashboardAppService service) : ShikshaMitraController
{
    [HttpGet("overview")]
    public Task<FinanceDashboardOverviewDto> Overview([FromQuery] Guid academicYearId,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken) =>
        service.GetOverviewAsync(academicYearId, from, to, cancellationToken);

    [HttpGet("activity")]
    public Task<FinanceDashboardActivityDto> Activity([FromQuery] Guid academicYearId, [FromQuery] DateOnly from,
        [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        service.GetActivityAsync(academicYearId, from, to, cancellationToken);
}
