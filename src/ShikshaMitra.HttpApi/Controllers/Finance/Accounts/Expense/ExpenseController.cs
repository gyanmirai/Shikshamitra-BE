using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Expense;

[RemoteService]
[Area("Finance")]
[Route("api/expenses")]
[Authorize]
public class ExpenseController(ITransactionAppService _transactionAppService) : AbpController
{
	/// <summary>
	/// Get expense list (paginated)
	/// </summary>
	/// <param name="academicYearId">Academic year identifier for filtering expenses</param>
	/// <param name="page">Page number for pagination</param>
	/// <param name="pageSize">Number of records per page</param>
	/// <param name="search">Search keyword for filtering expenses by name</param>
	/// <returns>Paginated expense list</returns>
	[HttpGet]
	public Task<TransactionListResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var request = new GetTransactionsRequest
		{
			Page = page,
			PageSize = pageSize,
			Search = search
		};

		return _transactionAppService.GetListAsync(academicYearId, request, TransactionType.Expense);
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
		[FromBody] TransactionCreateRequest input)
	{
		var result = await _transactionAppService.CreateAsync(academicYearId, input, TransactionType.Expense);
		return StatusCode(StatusCodes.Status201Created, result);
	}

	/// <summary>
	/// Get expense details by ID
	/// </summary>
	/// <param name="expenseId">Unique identifier of the expense transaction</param>
	/// <returns>Expense details with category information</returns>
	[HttpGet("{expense-id}")]
	public Task<TransactionDetailsResponse> GetByIdAsync([FromRoute(Name = "expense-id")] Guid expenseId)
	{
		return _transactionAppService.GetByIdAsync(expenseId, TransactionType.Expense);
	}

	/// <summary>
	/// Update expense
	/// </summary>
	/// <param name="expenseId">Unique identifier of the expense transaction</param>
	/// <param name="academicYearId">Academic year identifier</param>
	/// <param name="input">Expense update details</param>
	/// <returns>Updated expense details</returns>
	[HttpPut("{expense-id}")]
	public Task<TransactionCreateUpdateResponse> UpdateAsync(
		[FromRoute(Name = "expense-id")] Guid expenseId,
		[FromBody] TransactionUpdateRequest input)
	{
		return _transactionAppService.UpdateAsync(expenseId, input, TransactionType.Expense);
	}

	/// <summary>
	/// Delete expense (soft delete)
	/// </summary>
	/// <param name="expenseId">Unique identifier of the expense transaction</param>
	/// <returns>Success response</returns>
	[HttpDelete("{expense-id}")]
	public Task<TransactionDeleteResponse> DeleteAsync([FromRoute(Name = "expense-id")] Guid expenseId)
	{
		return _transactionAppService.DeleteAsync(expenseId, TransactionType.Expense);
	}
}
