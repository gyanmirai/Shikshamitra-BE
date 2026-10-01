using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Activity;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Activity;

/// <summary>
/// Scoped read-only register of Fees source activity and legacy manual entries.
/// A row marked Not posted is not a General Accounts journal entry.
/// </summary>
[ApiController]
[Area("finance")]
[Route("api/accounts/register")]
[Authorize]
public class AccountsRegisterController(IAccountsActivityAppService service) : AbpControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AccountsActivityPageDto), StatusCodes.Status200OK)]
    public Task<AccountsActivityPageDto> GetListAsync([FromQuery] AccountsActivityInput input)
        => service.GetListAsync(input);
}
