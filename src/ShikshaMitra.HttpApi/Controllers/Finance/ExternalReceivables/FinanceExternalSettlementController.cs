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
public class FinanceExternalSettlementController(IFinanceExternalSettlementAppService settlements) : AbpControllerBase
{
	[HttpGet("collect-form")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	[ProducesResponseType(typeof(FinanceExternalCollectionFormDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalCollectionFormDto> GetCollectFormAsync([FromQuery] FinanceExternalSourceReference source) =>
		settlements.GetCollectFormAsync(source);

	[HttpPost("settlement-policy")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	[ProducesResponseType(typeof(FinanceExternalCollectionFormDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalCollectionFormDto> BindPolicyAsync([FromBody] BindFinanceExternalSettlementPolicyInput input) =>
		settlements.BindPolicyAsync(input);

	[HttpPost("approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	[ProducesResponseType(typeof(FinanceExternalReceivableDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalReceivableDto> ApproveAsync([FromBody] FinanceExternalApprovalInput input) =>
		settlements.ApproveAsync(input);

	[HttpPost("collections")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	[ProducesResponseType(typeof(FinanceExternalCollectionDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalCollectionDto> CollectAsync([FromBody] FinanceExternalCollectInput input) =>
		settlements.CollectAsync(input);

	[HttpPost("waivers")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverDto> RequestWaiverAsync([FromBody] FinanceExternalWaiverInput input) =>
		settlements.RequestWaiverAsync(input);

	[HttpPost("waivers/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverDto> ApproveWaiverAsync([FromBody] FinanceExternalWaiverDecisionInput input) =>
		settlements.ApproveWaiverAsync(input);

	[HttpPost("waivers/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverDto> RejectWaiverAsync([FromBody] FinanceExternalWaiverDecisionInput input) =>
		settlements.RejectWaiverAsync(input);

	[HttpPost("waivers/reverse")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverDto> ReverseWaiverAsync([FromBody] ReverseFinanceExternalWaiverInput input) =>
		settlements.ReverseWaiverAsync(input);
}
