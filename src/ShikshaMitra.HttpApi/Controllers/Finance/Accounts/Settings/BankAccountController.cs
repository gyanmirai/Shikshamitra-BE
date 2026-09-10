using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;

[Area("finance")]
[Route("api/bank-accounts")]
public class BankAccountController : AbpController
{
	private readonly IBankAccountAppService _bankAccountAppService;

	public BankAccountController(IBankAccountAppService bankAccountAppService)
	{
		_bankAccountAppService = bankAccountAppService;
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.Add)]
	public Task<BankAccountResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateBankAccountRequest input)
	{
		return _bankAccountAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
	public Task<PaginatedBankAccountResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null,
		[FromQuery] bool includeInactive = true)
	{
		return _bankAccountAppService.GetListAsync(academicYearId, page, pageSize, search, includeInactive);
	}

	[HttpGet("{bankAccountId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
	public Task<BankAccountResponse> GetAsync(Guid bankAccountId)
	{
		return _bankAccountAppService.GetAsync(bankAccountId);
	}

	[HttpPut("{bankAccountId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.Edit)]
	public Task<BankAccountResponse> UpdateAsync(Guid bankAccountId, [FromBody] UpdateBankAccountRequest input)
	{
		return _bankAccountAppService.UpdateAsync(bankAccountId, input);
	}

	[HttpDelete("{bankAccountId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.Delete)]
	public Task DeleteAsync(Guid bankAccountId)
	{
		return _bankAccountAppService.DeleteAsync(bankAccountId);
	}

	[HttpPatch("{bankAccountId:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.ChangeStatus)]
	public Task<BankAccountResponse> UpdateStatusAsync(Guid bankAccountId, [FromBody] AccountSettingStatusUpdateRequest input) =>
		_bankAccountAppService.UpdateStatusAsync(bankAccountId, input);

	[HttpGet("{bankAccountId:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_BankAccount.View)]
	public Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid bankAccountId,
		[FromQuery] int skipCount = 0, [FromQuery] int maxResultCount = 50) =>
		_bankAccountAppService.GetVersionHistoryAsync(bankAccountId, skipCount, maxResultCount);
}
