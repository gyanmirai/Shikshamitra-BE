using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts.Expenses;
using ShikshaMitra.Finance.Accounts.Expenses.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Expense;

[RemoteService]
[Area("Finance")]
[Route("api/expenses")]
[Authorize]
public class ExpenseController : AbpController
{
	private readonly IExpenseAppService _expenseAppService;

	public ExpenseController(IExpenseAppService expenseAppService)
	{
		_expenseAppService = expenseAppService;
	}

	/// <summary>
	/// Get expense list (paginated)
	/// </summary>
	/// <param name="academicYearId">Academic year identifier for filtering expenses</param>
	/// <param name="page">Page number for pagination</param>
	/// <param name="pageSize">Number of records per page</param>
	/// <param name="search">Search keyword for filtering expenses by name</param>
	/// <returns>Paginated expense list</returns>
	[HttpGet]
	public Task<ExpenseListResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var request = new GetExpensesRequest
		{
			Page = page,
			PageSize = pageSize,
			Search = search
		};

		return _expenseAppService.GetListAsync(academicYearId, request);
	}

	/// <summary>
	/// Create expense
	/// </summary>
	/// <param name="academicYearId">Academic year identifier</param>
	/// <param name="input">Expense creation details</param>
	/// <returns>Created expense details</returns>
	[HttpPost]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] ExpenseCreateRequest input)
	{
		var result = await _expenseAppService.CreateAsync(academicYearId, input);
		return StatusCode(StatusCodes.Status201Created, result);
	}

	/// <summary>
	/// Get expense details by ID
	/// </summary>
	/// <param name="expenseId">Unique identifier of the expense transaction</param>
	/// <returns>Expense details with category information</returns>
	[HttpGet("{expense-id}")]
	public Task<ExpenseDetailsResponse> GetByIdAsync([FromRoute(Name = "expense-id")] Guid expenseId)
	{
		return _expenseAppService.GetByIdAsync(expenseId);
	}

	/// <summary>
	/// Update expense
	/// </summary>
	/// <param name="expenseId">Unique identifier of the expense transaction</param>
	/// <param name="academicYearId">Academic year identifier</param>
	/// <param name="input">Expense update details</param>
	/// <returns>Updated expense details</returns>
	[HttpPut("{expense-id}")]
	public Task<ExpenseCreateUpdateResponse> UpdateAsync(
		[FromRoute(Name = "expense-id")] Guid expenseId,
		[FromBody] ExpenseUpdateRequest input)
	{
		return _expenseAppService.UpdateAsync(expenseId, input);
	}

	/// <summary>
	/// Delete expense (soft delete)
	/// </summary>
	/// <param name="expenseId">Unique identifier of the expense transaction</param>
	/// <returns>Success response</returns>
	[HttpDelete("{expense-id}")]
	public Task<ExpenseDeleteResponse> DeleteAsync([FromRoute(Name = "expense-id")] Guid expenseId)
	{
		return _expenseAppService.DeleteAsync(expenseId);
	}
}
