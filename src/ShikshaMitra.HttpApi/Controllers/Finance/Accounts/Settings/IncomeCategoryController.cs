using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts.Settings.Dtos;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;

[RemoteService]
[Area("finance")]
[Route("api/income-categories")]
public class IncomeCategoryController(ITransactionCategoryAppService incomeCategoryAppService) : AbpController
{
	[HttpGet("account-head-options")]
	[Authorize]
	public Task<System.Collections.Generic.List<AccountHeadOptionDto>> GetAccountHeadOptionsAsync([FromQuery] Guid academicYearId) =>
		incomeCategoryAppService.GetAccountHeadOptionsAsync(academicYearId, TransactionType.Income);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.Add)]
	public Task<TransactionCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateIncomeCategoryRequest input)
	{
		return incomeCategoryAppService.CreateAsync(academicYearId, input, TransactionType.Income);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.View)]
	public Task<PaginatedTransactionCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null,
		[FromQuery] bool includeInactive = true,
		[FromQuery] string? filters = null)
	{
		return incomeCategoryAppService.GetListAsync(academicYearId, TransactionType.Income, page, pageSize, search, includeInactive, filters);
	}

	[HttpGet("{incomeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.View)]
	public Task<TransactionCategoryResponse> GetAsync(Guid incomeCategoryId)
	{
		return incomeCategoryAppService.GetAsync(incomeCategoryId, TransactionType.Income);
	}

	[HttpPut("{incomeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.Edit)]
	public Task<TransactionCategoryResponse> UpdateAsync(Guid incomeCategoryId, [FromBody] UpdateIncomeCategoryRequest input)
	{
		return incomeCategoryAppService.UpdateAsync(incomeCategoryId, input, TransactionType.Income);
	}

	[HttpDelete("{incomeCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.Delete)]
	public Task DeleteAsync(Guid incomeCategoryId)
	{
		return incomeCategoryAppService.DeleteAsync(incomeCategoryId, TransactionType.Income);
	}

	[HttpPatch("{incomeCategoryId:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.ChangeStatus)]
	public Task<TransactionCategoryResponse> UpdateStatusAsync(Guid incomeCategoryId, [FromBody] AccountSettingStatusUpdateRequest input) =>
		incomeCategoryAppService.UpdateStatusAsync(incomeCategoryId, input, TransactionType.Income);

	[HttpGet("{incomeCategoryId:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_IncomeCategory.View)]
	public Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid incomeCategoryId,
		[FromQuery] int skipCount = 0, [FromQuery] int maxResultCount = 50) =>
		incomeCategoryAppService.GetVersionHistoryAsync(incomeCategoryId, TransactionType.Income, skipCount, maxResultCount);
}
