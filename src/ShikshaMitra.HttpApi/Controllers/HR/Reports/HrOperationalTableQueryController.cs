using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Reports;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Reports;

[Authorize, Area("app"), RemoteService(Name = "Default"), Route("api/hr/table-query")]
public class HrOperationalTableQueryController(IHrOperationalTableQueryAppService tables) : AbpControllerBase
{
	[HttpPost] public Task<JsonElement> QueryAsync(HrOperationalTableQueryRequest request) => tables.QueryAsync(request);
}
