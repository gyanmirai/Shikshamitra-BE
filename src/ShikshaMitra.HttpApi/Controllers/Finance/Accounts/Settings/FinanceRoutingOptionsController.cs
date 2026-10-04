using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Settings;
using Volo.Abp.AspNetCore.Mvc;
namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;
[Authorize, Route("api/finance-routing-options")]
public class FinanceRoutingOptionsController(IFinanceRoutingOptionsAppService service) : AbpController
{
 [HttpGet] public Task<FinanceRoutingOptions> GetAsync([FromQuery] Guid academicYearId, [FromQuery] string operation) => service.GetAsync(academicYearId,operation);
}
