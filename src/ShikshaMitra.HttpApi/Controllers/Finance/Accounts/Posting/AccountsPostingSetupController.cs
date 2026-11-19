using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Posting;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Posting;

[ApiController]
[Area("finance")]
[Route("api/accounts/posting-setup")]
[Authorize]
public class AccountsPostingSetupController(IAccountsPostingSetupAppService service) : AbpControllerBase
{
    [HttpGet]
    [HttpGet("/api/accounts/readiness")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.View)]
    public Task<AccountsPostingSetupDto> GetAsync([FromQuery] Guid academicYearId) => service.GetAsync(academicYearId);

    [HttpPut]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    public Task<AccountsPostingSetupDto> ConfigureAsync([FromQuery] Guid academicYearId,
        [FromBody] ConfigureAccountsPostingDto input) => service.ConfigureAsync(academicYearId, input);

    [HttpPost("periods")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    public Task<AccountsPostingSetupDto> AddPeriodAsync([FromQuery] Guid academicYearId,
        [FromBody] CreateAccountingPeriodDto input) => service.AddPeriodAsync(academicYearId, input);

    [HttpPost("destinations")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    public Task<AccountsPostingSetupDto> AddDestinationAsync([FromQuery] Guid academicYearId,
        [FromBody] CreatePaymentDestinationDto input) => service.AddDestinationAsync(academicYearId, input);

    [HttpPost("destinations/{id:guid}/end")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_PostingSetup.Edit)]
    public Task<AccountsPostingSetupDto> EndDestinationAsync([FromQuery] Guid academicYearId,
        Guid id, [FromBody] EndPaymentDestinationDto input) =>
        service.EndDestinationAsync(academicYearId, id, input);
}
