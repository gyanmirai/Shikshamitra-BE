using System;
using System.Threading.Tasks;
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

[Area("finance")]
[Route("api/expense-categories")]
public class ExpenseCategoryController(ITransactionCategoryAppService expenseCategoryAppService) : AbpController
{
	[HttpGet("account-head-options")]
	[Authorize]
	public Task<System.Collections.Generic.List<AccountHeadOptionDto>> GetAccountHeadOptionsAsync([FromQuery] Guid academicYearId) =>
		expenseCategoryAppService.GetAccountHeadOptionsAsync(academicYearId, TransactionType.Expense);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.Add)]
	public Task<TransactionCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateTransactionCategoryRequest input)
	{
		return expenseCategoryAppService.CreateAsync(academicYearId, input, TransactionType.Expense);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.View)]
	public Task<PaginatedTransactionCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null,
		[FromQuery] bool includeInactive = true,
		[FromQuery] string? filters = null)
	{
		return expenseCategoryAppService.GetListAsync(academicYearId, TransactionType.Expense, page, pageSize, search, includeInactive, filters);
	}

	[HttpGet("{expenseCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.View)]
	public Task<TransactionCategoryResponse> GetAsync(Guid expenseCategoryId)
	{
		return expenseCategoryAppService.GetAsync(expenseCategoryId, TransactionType.Expense);
	}

	[HttpPut("{expenseCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.Edit)]
	public Task<TransactionCategoryResponse> UpdateAsync(Guid expenseCategoryId, [FromBody] UpdateTransactionCategoryRequest input)
	{
		return expenseCategoryAppService.UpdateAsync(expenseCategoryId, input, TransactionType.Expense);
	}

	[HttpDelete("{expenseCategoryId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.Delete)]
	public Task DeleteAsync(Guid expenseCategoryId)
	{
		return expenseCategoryAppService.DeleteAsync(expenseCategoryId, TransactionType.Expense);
	}

	[HttpPatch("{expenseCategoryId:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.ChangeStatus)]
	public Task<TransactionCategoryResponse> UpdateStatusAsync(Guid expenseCategoryId, [FromBody] AccountSettingStatusUpdateRequest input) =>
		expenseCategoryAppService.UpdateStatusAsync(expenseCategoryId, input, TransactionType.Expense);

	[HttpGet("{expenseCategoryId:guid}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Accounts_Settings_ExpenseCategory.View)]
	public Task<PagedResultDto<AccountSettingVersionHistoryItemDto>> GetVersionHistoryAsync(Guid expenseCategoryId,
		[FromQuery] int skipCount = 0, [FromQuery] int maxResultCount = 50) =>
		expenseCategoryAppService.GetVersionHistoryAsync(expenseCategoryId, TransactionType.Expense, skipCount, maxResultCount);
}
