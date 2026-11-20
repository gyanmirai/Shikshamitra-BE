using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Uow;

namespace ShikshaMitra.HttpApi.Controllers.Finance.ExternalReceivables;

[Authorize]
[ApiController]
[Area("finance")]
[RemoteService(Name = "Default")]
[Route("api/finance/external-receivables")]
[UnitOfWork(IsDisabled = true)]
public class FinanceExternalReceivableController(IFinanceExternalReceivableAppService receivables) : AbpControllerBase
{
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	[ProducesResponseType(typeof(FinanceExternalReceivableDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalReceivableDto> PostAsync([FromBody] PostFinanceExternalReceivableInput input) =>
		receivables.PostAsync(input);

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
	[ProducesResponseType(typeof(FinanceExternalReceivableDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalReceivableDto> GetAsync([FromQuery] FinanceExternalSourceReference source) =>
		receivables.GetAsync(source);
}
