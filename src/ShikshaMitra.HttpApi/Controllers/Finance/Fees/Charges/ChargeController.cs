using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Charges;

[RemoteService]
[Area("app")]
[Route("api/app/charges")]
[Authorize]
public class ChargeController : AbpController
{
	private const int MaxChargePageSize = 100;

	private readonly IChargeAppService _chargeAppService;

	public ChargeController(IChargeAppService chargeAppService)
	{
		_chargeAppService = chargeAppService;
	}

	/// <summary>
	/// Create a new charge for a student.
	/// Scope (board, medium, class, division) is derived server-side from the student record.
	/// </summary>
	[HttpPost]
	[ProducesResponseType(typeof(ChargeResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task<ChargeResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] CreateChargeRequest input)
	{
		return _chargeAppService.CreateAsync(academicYearId, input);
	}

	/// <summary>
	/// Retrieve a paginated list of charges with optional filtering and sorting.
	/// Maximum page size is 100.
	/// </summary>
	[HttpGet]
	[ProducesResponseType(typeof(ChargeListResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
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
			PageSize = Math.Min(pageSize, MaxChargePageSize),
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
	/// Get detailed information about a specific charge.
	/// </summary>
	[HttpGet("{chargeId}")]
	[ProducesResponseType(typeof(ChargeDetailResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task<ChargeDetailResponse> GetByIdAsync(Guid chargeId)
	{
		return _chargeAppService.GetByIdAsync(chargeId);
	}

	/// <summary>
	/// Update an existing charge. Only PENDING charges can be updated.
	/// </summary>
	[HttpPut("{chargeId}")]
	[ProducesResponseType(typeof(ChargeResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task<ChargeResponse> UpdateAsync(Guid chargeId, [FromBody] UpdateChargeRequest input)
	{
		return _chargeAppService.UpdateAsync(chargeId, input);
	}

	/// <summary>
	/// Approve a pending charge. Approver must differ from the requestor (maker-checker).
	/// </summary>
	[HttpPost("{chargeId}/approve")]
	[ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task<ApprovalResponse> ApproveAsync(Guid chargeId, [FromBody] ApproveChargeRequest input)
	{
		return _chargeAppService.ApproveAsync(chargeId, input);
	}

	/// <summary>
	/// Reject a pending charge with a mandatory reason. Rejector must differ from the requestor.
	/// </summary>
	[HttpPost("{chargeId}/reject")]
	[ProducesResponseType(typeof(RejectionResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task<RejectionResponse> RejectAsync(Guid chargeId, [FromBody] RejectChargeRequest input)
	{
		return _chargeAppService.RejectAsync(chargeId, input);
	}

	/// <summary>
	/// Delete a charge. Only PENDING charges can be deleted.
	/// </summary>
	[HttpDelete("{chargeId}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task DeleteAsync(Guid chargeId)
	{
		return _chargeAppService.DeleteAsync(chargeId);
	}
}
