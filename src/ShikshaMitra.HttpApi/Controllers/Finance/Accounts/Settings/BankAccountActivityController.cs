using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.BankActivity;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Content;
namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;
[Area("app"), RemoteService(Name = "Default")]
[Route("api/bank-accounts/{id:guid}")]
[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
public class BankAccountActivityController(IBankAccountActivityAppService activity) : AbpController
{
	[HttpGet("activity")]
	public Task<BankActivityPage> GetAsync(Guid id, [FromQuery] BankActivityQuery query) => activity.GetAsync(id, query);
	[HttpGet("deposits")]
	public Task<BankActivityPage> GetDepositsAsync(Guid id, [FromQuery] BankActivityQuery query) => activity.GetDepositsAsync(id, query);
	[HttpGet("activity/{movementKey}")]
	public Task<BankActivityDetail> GetDetailAsync(Guid id, string movementKey, [FromQuery] Guid academicYearId) => activity.GetDetailAsync(id, movementKey, academicYearId);
	[HttpPost("activity/export")]
	public Task<IRemoteStreamContent> ExportAsync(Guid id, [FromBody] BankActivityExportRequest input) => activity.ExportAsync(id, input);
}
