using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory;
using ShikshaMitra.Application.Contracts.Finance.Setting.ExpenseCategory.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Setting;

[Area("Finance")]
[Route("api/expense-categories")]
public class ExpenseCategoryController : AbpController
{
	private readonly IExpenseCategoryAppService _expenseCategoryAppService;

	public ExpenseCategoryController(IExpenseCategoryAppService expenseCategoryAppService)
	{
		_expenseCategoryAppService = expenseCategoryAppService;
	}

	[HttpPost]
	public Task<ExpenseCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateExpenseCategoryRequest input)
	{
		return _expenseCategoryAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet]
	public Task<PaginatedExpenseCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		return _expenseCategoryAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	[HttpGet("{expenseCategoryId}")]
	public Task<ExpenseCategoryResponse> GetAsync(Guid expenseCategoryId)
	{
		return _expenseCategoryAppService.GetAsync(expenseCategoryId);
	}

	[HttpPut("{expenseCategoryId}")]
	public Task<ExpenseCategoryResponse> UpdateAsync(Guid expenseCategoryId, [FromBody] UpdateExpenseCategoryRequest input)
	{
		return _expenseCategoryAppService.UpdateAsync(expenseCategoryId, input);
	}

	[HttpDelete("{expenseCategoryId}")]
	public Task DeleteAsync(Guid expenseCategoryId)
	{
		return _expenseCategoryAppService.DeleteAsync(expenseCategoryId);
	}
}
