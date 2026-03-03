using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests;
using ShikshaMitra.Application.Contracts.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.HttpApi.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/concession-request")]
public class ConcessionRequestController : ControllerBase
{
	private readonly IConcessionRequestAppService _appService;

	public ConcessionRequestController(IConcessionRequestAppService appService)
	{
		_appService = appService;
	}

	// ─── Step 1+2 merged — POST /api/concession-request ─────────────────────

	[HttpPost]
	[ProducesResponseType(StatusCodes.Status201Created)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> CreateConcessionRequest([FromBody] CreateConcessionRequestInput input)
	{
		var result = await _appService.CreateConcessionRequestAsync(input);
		return StatusCode(201, result);
	}

	// ─── GET /api/concession-request/{id} ────────────────────────────────────

	[HttpGet("{concessionRequestId:guid}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetBasicDetails([FromRoute] Guid concessionRequestId)
	{
		var result = await _appService.GetBasicDetailsAsync(concessionRequestId);
		return Ok(result);
	}

	// ─── Step 1+2 merged — PUT /api/concession-request/{id} ──────────────────

	[HttpPut("{concessionRequestId:guid}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
	public async Task<IActionResult> UpdateConcessionRequest(
		[FromRoute] Guid concessionRequestId,
		[FromBody] UpdateConcessionRequestInput input)
	{
		var result = await _appService.UpdateConcessionRequestAsync(concessionRequestId, input);
		return Ok(result);
	}

	// ─── Step 3 — PUT /api/concession-request/{id}/fee-heads ─────────────────

	[HttpPut("{concessionRequestId:guid}/fee-heads")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SaveFeeHeadsData(
		[FromRoute] Guid concessionRequestId,
		[FromBody] SaveFeeHeadsDataRequest input)
	{
		var result = await _appService.SaveFeeHeadsDataAsync(concessionRequestId, input);
		return Ok(result);
	}

	// ─── Step 4 — PUT /api/concession-request/{id}/value-entry ───────────────

	[HttpPut("{concessionRequestId:guid}/value-entry")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> SaveValueEntry(
		[FromRoute] Guid concessionRequestId,
		[FromBody] SaveValueEntryRequest input)
	{
		var result = await _appService.SaveValueEntryAsync(concessionRequestId, input);
		return Ok(result);
	}

	// ─── Step 5 — PUT /api/concession-request/{id}/document-upload ───────────

	[HttpPut("{concessionRequestId:guid}/document-upload")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> UploadDocuments(
		[FromRoute] Guid concessionRequestId,
		[FromBody] UploadDocumentsRequest input)
	{
		var result = await _appService.UploadDocumentsAsync(concessionRequestId, input);
		return Ok(result);
	}

	// ─── GET /api/concession-request/{id}/documents ───────────────────────────

	[HttpGet("{studentConcessionTypeRequestId:guid}/documents")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetDocuments(
		[FromRoute] Guid studentConcessionTypeRequestId,
		[FromQuery] int? status = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20)
	{
		var result = await _appService.GetConcessionDocumentsAsync(
			studentConcessionTypeRequestId, status, page, pageSize);
		return Ok(result);
	}

	// ─── GET /api/concession-request/{id}/fee-heads (Step 3 Edit/Resume) ──────

	[HttpGet("{concessionRequestId:guid}/fee-heads")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetFeeHeads([FromRoute] Guid concessionRequestId)
	{
		var result = await _appService.GetConcessionRequestFeeHeadsAsync(concessionRequestId);
		return Ok(result);
	}

	// ─── GET /api/concession-request/{id}/value-entries (Step 4 Edit/Resume) ──

	[HttpGet("{concessionRequestId:guid}/value-entries")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetValueEntries([FromRoute] Guid concessionRequestId)
	{
		var result = await _appService.GetConcessionRequestValueEntriesAsync(concessionRequestId);
		return Ok(result);
	}

	// ─── GET /api/concession-request/{id}/document-upload (Step 5 Edit/Resume) ─

	[HttpGet("{concessionRequestId:guid}/document-upload")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetDocumentUpload([FromRoute] Guid concessionRequestId)
	{
		var result = await _appService.GetConcessionRequestDocumentsForResumeAsync(concessionRequestId);
		return Ok(result);
	}

	// ─── GET /api/concession-request/students/{studentId} ────────────────────

	[HttpGet("students/{studentId:guid}")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetStudentConcessionRequests(
		[FromRoute] Guid studentId,
		[FromQuery] int? status = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var result = await _appService.GetStudentConcessionRequestsAsync(studentId, status, page, pageSize, search);
		return Ok(result);
	}

	// ─── POST /api/concession-request/{id}/approve ───────────────────────────

	[HttpPost("{studentConcessionTypeRequestId:guid}/approve")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Approve(
		[FromRoute] Guid studentConcessionTypeRequestId,
		[FromBody] ApprovalRequest input)
	{
		var result = await _appService.ApproveConcessionRequestAsync(studentConcessionTypeRequestId, input);
		return Ok(result);
	}

	// ─── POST /api/concession-request/{id}/reject ────────────────────────────

	[HttpPost("{studentConcessionTypeRequestId:guid}/reject")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Reject(
		[FromRoute] Guid studentConcessionTypeRequestId,
		[FromBody] RejectionRequest input)
	{
		var result = await _appService.RejectConcessionRequestAsync(studentConcessionTypeRequestId, input);
		return Ok(result);
	}

	// ─── GET /api/concession-request/applications ─────────────────────────────

	[HttpGet("applications")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> GetConcessionApplications(
		[FromQuery] Guid boardId,
		[FromQuery] Guid educationMediumId,
		[FromQuery] int? status = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		var result = await _appService.GetConcessionApplicationsAsync(
			boardId, educationMediumId, status, page, pageSize, search);
		return Ok(result);
	}

	// ─── DELETE /api/concession-request/{id} ─────────────────────────────────

	[HttpDelete("{studentConcessionTypeRequestId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Delete([FromRoute] Guid studentConcessionTypeRequestId)
	{
		await _appService.DeleteConcessionApplicationAsync(studentConcessionTypeRequestId);
		return NoContent();
	}
}
