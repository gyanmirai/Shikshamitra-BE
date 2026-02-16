using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.Charges;
using ShikshaMitra.Application.Contracts.Finance.Charges.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance;

[RemoteService]
[Area("Finance")]
[Route("api/charges")]
[Authorize]
public class ChargeController : AbpController
{
	private readonly IChargeAppService _chargeAppService;

	public ChargeController(IChargeAppService chargeAppService)
	{
		_chargeAppService = chargeAppService;
	}

	/// <summary>
	/// Create a new charge for a student
	/// </summary>
	/// <param name="academicYearId">Academic year identifier for data partitioning and filtering</param>
	/// <param name="input">Charge creation details</param>
	/// <returns>Created charge details</returns>
	[HttpPost]
	public Task<ChargeResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateChargeRequest input)
	{
		return _chargeAppService.CreateAsync(academicYearId, input);
	}

	/// <summary>
	/// Retrieve a paginated list of charges with optional filtering and sorting
	/// </summary>
	/// <param name="academicYearId">Academic year identifier for data partitioning and filtering</param>
	/// <param name="page">Page number for pagination (1-indexed)</param>
	/// <param name="pageSize">Number of records per page (maximum 100)</param>
	/// <param name="search">Search term for filtering by student name, GR number, or roll number</param>
	/// <param name="status">Filter by charge status (0=PENDING, 1=APPROVED, 2=REJECTED)</param>
	/// <param name="sortBy">Field to sort by</param>
	/// <param name="sortOrder">Sort order direction</param>
	/// <param name="educationMediumId">Filter by education medium</param>
	/// <param name="boardId">Filter by education board</param>
	/// <returns>Paginated list of charges</returns>
	[HttpGet]
	public Task<ChargeListResponse> GetAllAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null,
		[FromQuery] int status = 0,
		[FromQuery] string? sortBy = null,
		[FromQuery] string? sortOrder = "asc",
		[FromQuery] Guid? educationMediumId = null,
		[FromQuery] Guid? boardId = null)
	{
		var request = new GetChargesRequest
		{
			Page = page,
			PageSize = pageSize,
			Search = search,
			Status = status,
			SortBy = sortBy,
			SortOrder = sortOrder,
			EducationMediumId = educationMediumId,
			BoardId = boardId
		};

		return _chargeAppService.GetAllAsync(academicYearId, request);
	}

	/// <summary>
	/// Get detailed information about a specific charge
	/// </summary>
	/// <param name="chargeId">Unique charge identifier</param>
	/// <returns>Detailed charge information</returns>
	[HttpGet("{chargeId}")]
	public Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId)
	{
		return _chargeAppService.GetByIdAsync(chargeId);
	}

	/// <summary>
	/// Update an existing charge. Only PENDING charges can be updated.
	/// </summary>
	/// <param name="chargeId">Unique charge identifier</param>
	/// <param name="input">Charge update details</param>
	/// <returns>Updated charge details</returns>
	[HttpPut("{chargeId}")]
	public Task<ChargeResponse> UpdateAsync(Guid chargeId, [FromBody] UpdateChargeRequest input)
	{
		return _chargeAppService.UpdateAsync(chargeId, input);
	}

	/// <summary>
	/// Approve a pending charge
	/// </summary>
	/// <param name="chargeId">Unique charge identifier</param>
	/// <param name="input">Approval details</param>
	/// <returns>Approval response</returns>
	[HttpPost("{chargeId}/approve")]
	public Task<ApprovalResponse> ApproveAsync(Guid chargeId, [FromBody] ApproveChargeRequest input)
	{
		return _chargeAppService.ApproveAsync(chargeId, input);
	}

	/// <summary>
	/// Reject a pending charge with mandatory rejection reason
	/// </summary>
	/// <param name="chargeId">Unique charge identifier</param>
	/// <param name="input">Rejection details with reason</param>
	/// <returns>Rejection response</returns>
	[HttpPost("{chargeId}/reject")]
	public Task<RejectionResponse> RejectAsync(Guid chargeId, [FromBody] RejectChargeRequest input)
	{
		return _chargeAppService.RejectAsync(chargeId, input);
	}
}
