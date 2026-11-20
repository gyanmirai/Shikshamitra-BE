using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.ExternalReceivables.Operator;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.ExternalReceivables;

[Authorize]
[ApiController]
[Area("finance")]
[RemoteService(Name = "Default")]
[Route("api/finance/external-receivables/operator")]
public class FinanceExternalOperatorController(IFinanceExternalOperatorAppService operators) : AbpControllerBase
{
	[HttpGet("posting-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Add)]
	[ProducesResponseType(typeof(PagedResultDto<FinanceExternalOperatorOptionDto>), StatusCodes.Status200OK)]
	public Task<PagedResultDto<FinanceExternalOperatorOptionDto>> GetPostingOptionsAsync(
		[FromQuery] FinanceExternalSourceReference source, [FromQuery] FinanceExternalOperatorOptionKind optionKind,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
		operators.GetPostingOptionsAsync(source, optionKind, page, pageSize, search);

	[HttpGet("binding-options")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	[ProducesResponseType(typeof(PagedResultDto<FinanceExternalOperatorOptionDto>), StatusCodes.Status200OK)]
	public Task<PagedResultDto<FinanceExternalOperatorOptionDto>> GetBindingOptionsAsync(
		[FromQuery] FinanceExternalSourceReference source, [FromQuery] FinanceExternalOperatorOptionKind optionKind,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
		operators.GetBindingOptionsAsync(source, optionKind, page, pageSize, search);

	[HttpGet("binding-context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update)]
	[ProducesResponseType(typeof(FinanceExternalOperatorContextDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalOperatorContextDto> GetBindingContextAsync([FromQuery] FinanceExternalSourceReference source) =>
		operators.GetBindingContextAsync(source);

	[HttpGet("approval-context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve)]
	[ProducesResponseType(typeof(FinanceExternalOperatorContextDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalOperatorContextDto> GetApprovalContextAsync([FromQuery] FinanceExternalSourceReference source) =>
		operators.GetApprovalContextAsync(source);

	[HttpGet("history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View)]
	[ProducesResponseType(typeof(PagedResultDto<FinanceExternalOperatorHistoryDto>), StatusCodes.Status200OK)]
	public Task<PagedResultDto<FinanceExternalOperatorHistoryDto>> GetHistoryAsync([FromQuery] FinanceExternalSourceReference source,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => operators.GetHistoryAsync(source, page, pageSize);

	[HttpGet("waiver-apply-context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverContextDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverContextDto> GetWaiverApplyContextAsync([FromQuery] FinanceExternalSourceReference source,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		operators.GetWaiverContextAsync(source, FinanceExternalWaiverAction.Apply, page, pageSize);

	[HttpGet("waiver-approval-context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverContextDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverContextDto> GetWaiverApprovalContextAsync([FromQuery] FinanceExternalSourceReference source,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		operators.GetWaiverContextAsync(source, FinanceExternalWaiverAction.Approve, page, pageSize);

	[HttpGet("waiver-rejection-context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverContextDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverContextDto> GetWaiverRejectionContextAsync([FromQuery] FinanceExternalSourceReference source,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		operators.GetWaiverContextAsync(source, FinanceExternalWaiverAction.Reject, page, pageSize);

	[HttpGet("waiver-reversal-context")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver)]
	[ProducesResponseType(typeof(FinanceExternalWaiverContextDto), StatusCodes.Status200OK)]
	public Task<FinanceExternalWaiverContextDto> GetWaiverReversalContextAsync([FromQuery] FinanceExternalSourceReference source,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		operators.GetWaiverContextAsync(source, FinanceExternalWaiverAction.Reverse, page, pageSize);
}
