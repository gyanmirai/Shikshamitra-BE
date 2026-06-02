using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;

[Area("Finance")]
[Route("api/expense-categories")]
public class ExpenseCategoryController(ITransactionCategoryAppService expenseCategoryAppService) : AbpController
{
	[HttpPost]
	public Task<TransactionCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateTransactionCategoryRequest input)
	{
		return expenseCategoryAppService.CreateAsync(academicYearId, input, TransactionType.Expense);
	}

	[HttpGet]
	public Task<PaginatedTransactionCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		return expenseCategoryAppService.GetListAsync(academicYearId, TransactionType.Expense, page, pageSize, search);
	}

	[HttpGet("{expenseCategoryId}")]
	public Task<TransactionCategoryResponse> GetAsync(Guid expenseCategoryId)
	{
		return expenseCategoryAppService.GetAsync(expenseCategoryId, TransactionType.Expense);
	}

	[HttpPut("{expenseCategoryId}")]
	public Task<TransactionCategoryResponse> UpdateAsync(Guid expenseCategoryId, [FromBody] UpdateTransactionCategoryRequest input)
	{
		return expenseCategoryAppService.UpdateAsync(expenseCategoryId, input, TransactionType.Expense);
	}

	[HttpDelete("{expenseCategoryId}")]
	public Task DeleteAsync(Guid expenseCategoryId)
	{
		return expenseCategoryAppService.DeleteAsync(expenseCategoryId, TransactionType.Expense);
	}
}
