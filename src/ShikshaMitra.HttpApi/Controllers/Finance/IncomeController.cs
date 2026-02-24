using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Incomes;
using ShikshaMitra.Application.Contracts.Finance.Incomes.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance;

[RemoteService]
[Area("Finance")]
[Route("api/incomes")]
[Authorize]
public class IncomeController : AbpController
{
	private readonly IIncomeAppService _incomeAppService;

	public IncomeController(IIncomeAppService incomeAppService)
	{
		_incomeAppService = incomeAppService;
	}

	/// <summary>
	/// Get income list (paginated)
	/// </summary>
	/// <param name="academicYearId">Academic year identifier for filtering incomes</param>
	/// <param name="page">Page number for pagination</param>
	/// <param name="pageSize">Number of records per page</param>
	/// <param name="search">Search keyword for filtering incomes by name</param>
	/// <returns>Paginated income list</returns>
	[HttpGet]
	public Task<IncomeListResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var request = new GetIncomesRequest
		{
			Page = page,
			PageSize = pageSize,
			Search = search
		};

		return _incomeAppService.GetListAsync(academicYearId, request);
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
		[FromBody] IncomeCreateRequest input)
	{
		var result = await _incomeAppService.CreateAsync(academicYearId, input);
		return StatusCode(StatusCodes.Status201Created, result);
	}

	/// <summary>
	/// Get income details by ID
	/// </summary>
	/// <param name="incomeId">Unique identifier of the income transaction</param>
	/// <returns>Income details with category information</returns>
	[HttpGet("{income-id}")]
	public Task<IncomeDetailsResponse> GetByIdAsync([FromRoute(Name = "income-id")] Guid incomeId)
	{
		return _incomeAppService.GetByIdAsync(incomeId);
	}

	/// <summary>
	/// Update income
	/// </summary>
	/// <param name="incomeId">Unique identifier of the income transaction</param>
	/// <param name="input">Income update details</param>
	/// <returns>Updated income details</returns>
	[HttpPut("{income-id}")]
	public Task<IncomeCreateUpdateResponse> UpdateAsync(
		[FromRoute(Name = "income-id")] Guid incomeId,
		[FromBody] IncomeUpdateRequest input)
	{
		return _incomeAppService.UpdateAsync(incomeId, input);
	}

	/// <summary>
	/// Delete income (soft delete)
	/// </summary>
	/// <param name="incomeId">Unique identifier of the income transaction</param>
	/// <returns>Success response</returns>
	[HttpDelete("{income-id}")]
	public Task<IncomeDeleteResponse> DeleteAsync([FromRoute(Name = "income-id")] Guid incomeId)
	{
		return _incomeAppService.DeleteAsync(incomeId);
	}
}
