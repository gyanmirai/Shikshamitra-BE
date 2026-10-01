using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Activity;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Activity;

[ApiController]
[Area("finance")]
[Route("api/accounts/activity")]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Activity.View)]
public class AccountsActivityController(IAccountsActivityAppService service) : AbpControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AccountsActivityPageDto), StatusCodes.Status200OK)]
    public Task<AccountsActivityPageDto> GetListAsync([FromQuery] AccountsActivityInput input)
        => service.GetListAsync(input);
}
