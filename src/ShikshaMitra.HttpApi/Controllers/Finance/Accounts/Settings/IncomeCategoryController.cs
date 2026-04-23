using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategory;
using ShikshaMitra.Finance.Accounts.Settings.TransactionCategory.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Settings;

[RemoteService]
[Area("Finance")]
[Route("api/income-categories")]
public class IncomeCategoryController : AbpController
{
	private readonly ITransactionCategoryAppService _incomeCategoryAppService;

	public IncomeCategoryController(ITransactionCategoryAppService incomeCategoryAppService)
	{
		_incomeCategoryAppService = incomeCategoryAppService;
	}

	[HttpPost]
	public Task<TransactionCategoryResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateTransactionCategoryRequest input)
	{
		return _incomeCategoryAppService.CreateAsync(academicYearId, input);
	}

	[HttpGet]
	public Task<PaginatedTransactionCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? search = null)
	{
		return _incomeCategoryAppService.GetListAsync(academicYearId, page, pageSize, search);
	}

	[HttpGet("{incomeCategoryId}")]
	public Task<TransactionCategoryResponse> GetAsync(Guid incomeCategoryId)
	{
		return _incomeCategoryAppService.GetAsync(incomeCategoryId);
	}

	[HttpPut("{incomeCategoryId}")]
	public Task<TransactionCategoryResponse> UpdateAsync(Guid incomeCategoryId, [FromBody] UpdateTransactionCategoryRequest input)
	{
		return _incomeCategoryAppService.UpdateAsync(incomeCategoryId, input);
	}

	[HttpDelete("{incomeCategoryId}")]
	public Task DeleteAsync(Guid incomeCategoryId)
	{
		return _incomeCategoryAppService.DeleteAsync(incomeCategoryId);
	}
}
