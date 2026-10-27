using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.CashManagement;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.CashManagement;

[Authorize, Area("app"), RemoteService(Name = "Default"), Route("api/finance/cash-management")]
public class CashManagementController(ICashManagementAppService service) : AbpController
{
	[HttpGet] public Task<CashPage> GetListAsync([FromQuery] CashQuery input) => service.GetListAsync(input);
	[HttpGet("options")] public Task<CashOptions> GetOptionsAsync([FromQuery] Guid academicYearId, [FromQuery] string operation) => service.GetOptionsAsync(academicYearId, operation);
	[HttpGet("{id:guid}")] public Task<CashDetail> GetAsync(Guid id, [FromQuery] Guid academicYearId) => service.GetAsync(id, academicYearId);
	[HttpPost("{id:guid}/approve")] public Task<CashDetail> ApproveAsync(Guid id, [FromBody] CashApproval input) => service.ApproveAsync(id, input);
	[HttpPost("{id:guid}/deposits")] public Task<CashDetail> DepositAsync(Guid id, [FromBody] CashDepositInput input) => service.DepositAsync(id, input);
	[HttpPost("{id:guid}/deposits/{depositId:guid}/confirm")] public Task<CashDetail> ConfirmAsync(Guid id, Guid depositId, [FromBody] CashCreditInput input) => service.ConfirmAsync(id, depositId, input);
}
