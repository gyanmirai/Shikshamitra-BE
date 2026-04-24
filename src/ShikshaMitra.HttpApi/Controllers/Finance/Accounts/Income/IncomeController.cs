using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Accounts.Transactions.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Accounts.Income;

[RemoteService]
[Area("Finance")]
[Route("api/incomes")]
[Authorize]
public class IncomeController(ITransactionAppService transactionAppService) : AbpController
{
	/// <summary>
	/// Get income list (paginated)
	/// </summary>
	/// <param name="academicYearId">Academic year identifier for filtering incomes</param>
	/// <param name="page">Page number for pagination</param>
	/// <param name="pageSize">Number of records per page</param>
	/// <param name="search">Search keyword for filtering incomes by name</param>
	/// <returns>Paginated transaction list</returns>
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

		return transactionAppService.GetListAsync(academicYearId, request, TransactionType.Income);
	}

	/// <summary>
	/// Create income
	/// </summary>
	/// <param name="academicYearId">Academic year identifier</param>
	/// <param name="input">Income creation details</param>
	/// <returns>Created income details</returns>
	[HttpPost]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] TransactionCreateRequest input)
	{
		var result = await transactionAppService.CreateAsync(academicYearId, input, TransactionType.Income);
		return StatusCode(StatusCodes.Status201Created, result);
	}

	/// <summary>
	/// Get income details by ID
	/// </summary>
	/// <param name="incomeId">Unique identifier of the income transaction</param>
	/// <returns>Income details with category information</returns>
	[HttpGet("{income-id}")]
	public Task<TransactionDetailsResponse> GetByIdAsync([FromRoute(Name = "income-id")] Guid incomeId)
	{
		return transactionAppService.GetByIdAsync(incomeId, TransactionType.Income);
	}

	/// <summary>
	/// Update income
	/// </summary>
	/// <param name="incomeId">Unique identifier of the income transaction</param>
	/// <param name="input">Income update details</param>
	/// <returns>Updated income details</returns>
	[HttpPut("{income-id}")]
	public Task<TransactionCreateUpdateResponse> UpdateAsync(
		[FromRoute(Name = "income-id")] Guid incomeId,
		[FromBody] TransactionUpdateRequest input)
	{
		return transactionAppService.UpdateAsync(incomeId, input, TransactionType.Income);
	}

	/// <summary>
	/// Delete income (soft delete)
	/// </summary>
	/// <param name="incomeId">Unique identifier of the income transaction</param>
	/// <returns>Success response</returns>
	[HttpDelete("{income-id}")]
	public Task<TransactionDeleteResponse> DeleteAsync([FromRoute(Name = "income-id")] Guid incomeId)
	{
		return transactionAppService.DeleteAsync(incomeId, TransactionType.Income);
	}
}
