using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance;

[ApiController]
[Route("api")]
public class ConcessionRequestController : AbpControllerBase
{
	private readonly IConcessionRequestAppService _concessionRequestAppService;

	public ConcessionRequestController(IConcessionRequestAppService concessionRequestAppService)
	{
		_concessionRequestAppService = concessionRequestAppService;
	}

	// ─── Step 1 POST ─────────────────────────────────────────────────────────

	/// <summary>Step 1: Create basic details for a concession request.</summary>
	[HttpPost("concession-request/basic-details")]
	[ProducesResponseType(typeof(CreateBasicDetailsResponse), 201)]
	public async Task<IActionResult> CreateBasicDetailsAsync([FromBody] CreateBasicDetailsRequest input)
	{
		var result = await _concessionRequestAppService.CreateBasicDetailsAsync(input);
		return StatusCode(201, result);
	}

	// ─── Step 1 GET ──────────────────────────────────────────────────────────

	/// <summary>Step 1: Get basic details of a concession request by ID.</summary>
	[HttpGet("concession-request/basic-details/{concessionRequestId:guid}")]
	[ProducesResponseType(typeof(GetBasicDetailsResponse), 200)]
	public async Task<GetBasicDetailsResponse> GetBasicDetailsAsync([FromRoute] Guid concessionRequestId)
	{
		return await _concessionRequestAppService.GetBasicDetailsAsync(concessionRequestId);
	}

	// ─── Step 2 PUT ──────────────────────────────────────────────────────────

	/// <summary>Step 2: Save selected concession types for a concession request.</summary>
	[HttpPut("concession-request/{concessionRequestId:guid}/concession-types")]
	[ProducesResponseType(typeof(SaveConcessionTypesResponse), 200)]
	public async Task<SaveConcessionTypesResponse> SaveConcessionTypesAsync(
		[FromRoute] Guid concessionRequestId,
		[FromBody] SaveConcessionTypesRequest input)
	{
		return await _concessionRequestAppService.SaveConcessionTypesAsync(concessionRequestId, input);
	}

	// ─── Step 3 PUT ──────────────────────────────────────────────────────────

	/// <summary>Step 3: Save fee head data for a concession request.</summary>
	[HttpPut("concession-requests/{concessionRequestId:guid}/feeHeadsData")]
	[ProducesResponseType(typeof(SaveFeeHeadsDataResponse), 200)]
	public async Task<SaveFeeHeadsDataResponse> SaveFeeHeadsDataAsync(
		[FromRoute] Guid concessionRequestId,
		[FromBody] SaveFeeHeadsDataRequest input)
	{
		return await _concessionRequestAppService.SaveFeeHeadsDataAsync(concessionRequestId, input);
	}

	// ─── Step 4 PUT ──────────────────────────────────────────────────────────

	/// <summary>Step 4: Save discount value entry for a concession request.</summary>
	[HttpPut("concession-requests/{concessionRequestId:guid}/value-entry")]
	[ProducesResponseType(typeof(SaveValueEntryResponse), 200)]
	public async Task<SaveValueEntryResponse> SaveValueEntryAsync(
		[FromRoute] Guid concessionRequestId,
		[FromBody] SaveValueEntryRequest input)
	{
		return await _concessionRequestAppService.SaveValueEntryAsync(concessionRequestId, input);
	}

	// ─── Get Student Concession Requests ─────────────────────────────────────

	/// <summary>Get all concession requests for a specific student.</summary>
	[HttpGet("concession-requests/{studentId:guid}")]
	[ProducesResponseType(typeof(ConcessionListResponse), 200)]
	public async Task<ConcessionListResponse> GetStudentConcessionRequestsAsync(
		[FromRoute] Guid studentId,
		[FromQuery] int? status = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		return await _concessionRequestAppService.GetStudentConcessionRequestsAsync(studentId, status, page, pageSize, search);
	}

	// ─── Approve ─────────────────────────────────────────────────────────────

	/// <summary>Approve a concession request.</summary>
	[HttpPost("concession-requests/{studentConcessionTypeRequestId:guid}/approve")]
	[ProducesResponseType(typeof(ApprovalResponse), 200)]
	public async Task<ApprovalResponse> ApproveConcessionRequestAsync(
		[FromRoute] Guid studentConcessionTypeRequestId,
		[FromBody] ApprovalRequest input)
	{
		return await _concessionRequestAppService.ApproveConcessionRequestAsync(studentConcessionTypeRequestId, input);
	}

	// ─── Reject ──────────────────────────────────────────────────────────────

	/// <summary>Reject a concession request.</summary>
	[HttpPost("concession-requests/{studentConcessionTypeRequestId:guid}/reject")]
	[ProducesResponseType(typeof(RejectionResponse), 200)]
	public async Task<RejectionResponse> RejectConcessionRequestAsync(
		[FromRoute] Guid studentConcessionTypeRequestId,
		[FromBody] RejectionRequest input)
	{
		return await _concessionRequestAppService.RejectConcessionRequestAsync(studentConcessionTypeRequestId, input);
	}

	// ─── Get Concession Applications ─────────────────────────────────────────

	/// <summary>Get all concession applications (admin view).</summary>
	[HttpGet("concession-applications")]
	[ProducesResponseType(typeof(ConcessionApplicationsResponse), 200)]
	public async Task<ConcessionApplicationsResponse> GetConcessionApplicationsAsync(
		[FromQuery] Guid boardId,
		[FromQuery] Guid educationMediumId,
		[FromQuery] int? status = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		return await _concessionRequestAppService.GetConcessionApplicationsAsync(
			boardId, educationMediumId, status, page, pageSize, search);
	}

	// ─── Delete ───────────────────────────────────────────────────────────────

	/// <summary>Delete a concession application.</summary>
	[HttpDelete("concession-requests/delete-concession/{studentConcessionTypeRequestId:guid}")]
	[ProducesResponseType(204)]
	public async Task<IActionResult> DeleteConcessionApplicationAsync(
		[FromRoute] Guid studentConcessionTypeRequestId)
	{
		await _concessionRequestAppService.DeleteConcessionApplicationAsync(studentConcessionTypeRequestId);
		return NoContent();
	}
}
