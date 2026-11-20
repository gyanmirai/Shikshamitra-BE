using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
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
public class FinanceExternalRefundController(IFinanceExternalRefundAppService refunds) : AbpControllerBase
{
	[HttpGet("refund-form")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
	[ProducesResponseType(typeof(FinanceExternalRefundFormDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalRefundFormDto> GetFormAsync([FromQuery] FinanceExternalSourceReference source) =>
		refunds.GetFormAsync(source);

	[HttpPost("refunds/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
	[ProducesResponseType(typeof(FeeRefundPreviewDto), StatusCodes.Status200OK)]
	public Task<FeeRefundPreviewDto> PreviewAsync([FromBody] FinanceExternalRefundSelection input) =>
		refunds.PreviewAsync(input);

	[HttpPost("refunds")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
	[ProducesResponseType(typeof(FeeRefundRequestDto), StatusCodes.Status200OK)]
	public Task<FeeRefundRequestDto> CreateAsync([FromBody] FinanceExternalRefundRequestInput input) =>
		refunds.CreateAsync(input);
}
