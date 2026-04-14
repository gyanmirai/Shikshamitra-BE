using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Controllers.Finance;

/// <summary>
/// Handles the six-step concession wizard (Steps 3–6) for a Draft request batch.
/// All routes are sub-resources of <c>api/concession-request/{concessionRequestId}</c>.
/// </summary>
[Authorize]
[Route("api/concession-request")]
public class ConcessionRequestWizardController : AbpController
{
	private readonly IConcessionRequestWizardAppService _appService;

	public ConcessionRequestWizardController(IConcessionRequestWizardAppService appService)
	{
		_appService = appService;
	}

	// -------------------------------------------------------------------------
	// Step 3 — Fee Heads
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 3 (Edit / Resume) — Returns the previously saved fee-head selections for every
	/// concession type in the student's request batch.
	/// Each entry carries concession-type metadata and an empty <c>feeHeadIds</c> list
	/// for types that have not yet had fee heads saved.
	/// </summary>
	[HttpGet("{concessionRequestId:guid}/fee-heads")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestFeeHeadsDto>), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<ApiDataResponseDto<ConcessionRequestFeeHeadsDto>> GetFeeHeadsAsync(
		Guid concessionRequestId)
		=> _appService.GetFeeHeadsAsync(concessionRequestId);

	/// <summary>
	/// Step 3 — Fee Head data (Save &amp; Continue). Full replacement operation.
	/// Replaces the fee-head selection for each listed concession type.
	/// </summary>
	[HttpPut("{concessionRequestId:guid}/feeHeadsData")]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task SaveFeeHeadsDataAsync(
		Guid concessionRequestId,
		[FromBody] SaveFeeHeadsDataDto input)
		=> _appService.SaveFeeHeadsDataAsync(concessionRequestId, input);

	// -------------------------------------------------------------------------
	// Step 4 — Value Entry
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 4 (Edit / Resume) — Returns the previously saved discount values
	/// (basedOn, amount, netAmount) per concession type per fee head.
	/// </summary>
	[HttpGet("{concessionRequestId:guid}/value-entries")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestValueEntriesDto>), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<ApiDataResponseDto<ConcessionRequestValueEntriesDto>> GetValueEntriesAsync(
		Guid concessionRequestId)
		=> _appService.GetValueEntriesAsync(concessionRequestId);

	/// <summary>
	/// Step 4 — Value Entry (Save &amp; Continue). Full replacement operation.
	/// Persists discount amounts or percentages per fee head, grouped by concession type.
	/// </summary>
	[HttpPut("{concessionRequestId:guid}/value-entry")]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task SaveValueEntryAsync(
		Guid concessionRequestId,
		[FromBody] ValueEntryDto input)
		=> _appService.SaveValueEntryAsync(concessionRequestId, input);

	// -------------------------------------------------------------------------
	// Step 5 — Document Upload
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 5 (Edit / Resume) — Returns all document template slots per concession type,
	/// each merged with previously saved upload data (URL, status, uploadedAt).
	/// Slots never submitted return url=null, status=0, uploadedAt=null.
	/// </summary>
	[HttpGet("{concessionRequestId:guid}/document-upload")]
	[ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDocumentsDto>), 200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task<ApiDataResponseDto<ConcessionRequestDocumentsDto>> GetDocumentUploadsAsync(
		Guid concessionRequestId)
		=> _appService.GetDocumentUploadsAsync(concessionRequestId);

	/// <summary>
	/// Step 5 — Document Upload (Save &amp; Continue). Full replacement operation.
	/// Persists document URLs and upload statuses per concession type.
	/// </summary>
	[HttpPut("{concessionRequestId:guid}/document-upload")]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task SaveDocumentUploadAsync(
		Guid concessionRequestId,
		[FromBody] UploadDocumentsDto input)
		=> _appService.SaveDocumentUploadAsync(concessionRequestId, input);

	// -------------------------------------------------------------------------
	// Step 6 — Description
	// -------------------------------------------------------------------------

	/// <summary>
	/// Step 6 — Save Description (optional). Saves a free-text description against a
	/// Draft concession request. Null or empty clears any previously saved description.
	/// </summary>
	[HttpPut("{concessionRequestId:guid}/description")]
	[ProducesResponseType(200)]
	[ProducesResponseType(401)]
	[ProducesResponseType(404)]
	public virtual Task SaveDescriptionAsync(
		Guid concessionRequestId,
		[FromBody] SaveDescriptionDto input)
		=> _appService.SaveDescriptionAsync(concessionRequestId, input);
}
