using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;
/// <summary>Legacy resource retired; historical references are retained internally.</summary>
[Authorize, Route("api/account-heads")]
public class AccountHeadController : AbpController
{
 [Route("{**path}"), AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
 public IActionResult Retired() => StatusCode(410, new { message = "Account Heads have been retired. Use Income Categories, Expense Categories and Bank Accounts." });
}
