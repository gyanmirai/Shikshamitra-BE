using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.TransactionCategory;
using ShikshaMitra.Application.Contracts.Finance.Setting.TransactionCategory.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Setting;

[Area("Finance")]
[Route("api/expense-categories")]
public class ExpenseCategoryController : AbpController
{
	private readonly ITransactionCategoryAppService _expenseCategoryAppService;

	public ExpenseCategoryController(ITransactionCategoryAppService expenseCategoryAppService)
	{
		_expenseCategoryAppService = expenseCategoryAppService;
	}

	[HttpPost]
	public Task<TransactionCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateTransactionCategoryRequest input)
	{
		return _expenseCategoryAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet]
	public Task<PaginatedTransactionCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		return _expenseCategoryAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	[HttpGet("{expenseCategoryId}")]
	public Task<TransactionCategoryResponse> GetAsync(Guid expenseCategoryId)
	{
		return _expenseCategoryAppService.GetAsync(expenseCategoryId);
	}

	[HttpPut("{expenseCategoryId}")]
	public Task<TransactionCategoryResponse> UpdateAsync(Guid expenseCategoryId, [FromBody] UpdateTransactionCategoryRequest input)
	{
		return _expenseCategoryAppService.UpdateAsync(expenseCategoryId, input);
	}

	[HttpDelete("{expenseCategoryId}")]
	public Task DeleteAsync(Guid expenseCategoryId)
	{
		return _expenseCategoryAppService.DeleteAsync(expenseCategoryId);
	}
}
