using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategories.Dtos;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;

[RemoteService]
[Area("Finance")]
[Route("api/income-categories")]
public class IncomeCategoryController(ITransactionCategoryAppService incomeCategoryAppService) : AbpController
{
	[HttpPost]
	public Task<TransactionCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateTransactionCategoryRequest input)
	{
		return incomeCategoryAppService.CreateAsync(academicYearId, input, TransactionType.Income);
	}

	[HttpGet]
	public Task<PaginatedTransactionCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		return incomeCategoryAppService.GetListAsync(academicYearId, TransactionType.Income, page, pageSize, search);
	}

	[HttpGet("{incomeCategoryId}")]
	public Task<TransactionCategoryResponse> GetAsync(Guid incomeCategoryId)
	{
		return incomeCategoryAppService.GetAsync(incomeCategoryId, TransactionType.Income);
	}

	[HttpPut("{incomeCategoryId}")]
	public Task<TransactionCategoryResponse> UpdateAsync(Guid incomeCategoryId, [FromBody] UpdateTransactionCategoryRequest input)
	{
		return incomeCategoryAppService.UpdateAsync(incomeCategoryId, input, TransactionType.Income);
	}

	[HttpDelete("{incomeCategoryId}")]
	public Task DeleteAsync(Guid incomeCategoryId)
	{
		return incomeCategoryAppService.DeleteAsync(incomeCategoryId, TransactionType.Income);
	}
}
