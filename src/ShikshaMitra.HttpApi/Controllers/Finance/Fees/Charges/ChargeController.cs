using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Charges.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Charges;

[RemoteService]
[Area("finance")]
[Route("api/charges")]
[Authorize]
public class ChargeController(IChargeAppService chargeAppService) : AbpController
{
	private const int MaxChargePageSize = 100;

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
		return chargeAppService.CreateAsync(academicYearId, input);
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
		[FromQuery] Guid? boardId = null,
		[FromQuery] Guid? standardId = null,
		[FromQuery] Guid? divisionId = null,
		[FromQuery] string? filters = null)
	{
		var request = new GetChargesRequest
		{
			AcademicYearId = academicYearId,
			Page = page,
			PageSize = Math.Min(pageSize, MaxChargePageSize),
			Search = search,
			Filters = filters,
			Status = status,
			SortBy = sortBy,
			SortOrder = sortOrder,
			EducationMediumId = educationMediumId,
			BoardId = boardId,
			StandardId = standardId,
			DivisionId = divisionId
		};

		return chargeAppService.GetAllAsync(request);
	}

	/// <summary>
	/// Retrieve flat charge lines for student-grouped history views.
	/// </summary>
	[HttpGet("lines")]
	[ProducesResponseType(typeof(ChargeLineListResponse), StatusCodes.Status200OK)]
	public Task<ChargeLineListResponse> GetLinesAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 100,
		[FromQuery] int? status = null,
		[FromQuery] Guid? educationMediumId = null,
		[FromQuery] Guid? boardId = null)
	{
		var request = new GetChargeLinesRequest
		{
			Page = page,
			PageSize = Math.Min(pageSize, MaxChargePageSize),
			Status = status,
			EducationMediumId = educationMediumId,
			BoardId = boardId
		};

		return chargeAppService.GetLinesAsync(academicYearId, request);
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
		return chargeAppService.GetByIdAsync(chargeId);
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
		return chargeAppService.UpdateAsync(chargeId, input);
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
		return chargeAppService.ApproveAsync(chargeId, input);
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
		return chargeAppService.RejectAsync(chargeId, input);
	}

	/// <summary>
	/// Void approved, unpaid charge lines while preserving their audit history.
	/// </summary>
	[HttpPost("{chargeId}/void")]
	[ProducesResponseType(typeof(ApprovalResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public Task<ApprovalResponse> VoidAsync(Guid chargeId, [FromBody] VoidChargeRequest input)
	{
		return chargeAppService.VoidAsync(chargeId, input);
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
		return chargeAppService.DeleteAsync(chargeId);
	}
}
