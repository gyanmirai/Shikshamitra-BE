using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Controllers.Finance;

[Authorize]
[Route("api/concession-request")]
public class ConcessionRequestController : AbpController
{
    private readonly IConcessionRequestAppService _appService;

    public ConcessionRequestController(IConcessionRequestAppService appService)
    {
        _appService = appService;
    }

    /// <summary>
    /// Gets a concession request by ID (Step 1 + Step 2 data).
    /// Returns the anchor request enriched with the student's enrollment context
    /// (board, medium, standard, division) and all concession-type selections
    /// that share the same lifecycle status.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestDetailDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public virtual Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>Returns a paged list of ConcessionRequests.</summary>
    [HttpGet]
    public virtual Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(
        [FromQuery] GetConcessionRequestListRequestDto input)
        => _appService.GetListAsync(input);

    /// <summary>Creates a new ConcessionRequest in Draft state.</summary>
    [HttpPost]
    public async Task CreateAsync([FromBody] CreateConcessionRequestDto input)
        => await _appService.CreateAsync(input);

    /// <summary>
    /// Replaces the concession-type selection for a student's Pending request batch.
    /// The <paramref name="id"/> must identify a Pending request owned by the student
    /// in the body; all Pending requests for that student are then replaced with the
    /// supplied <c>concessionTypeIds</c>.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ConcessionRequestDto> UpdateAsync(Guid id, [FromBody] UpdateConcessionRequestDto input)
        => await _appService.UpdateAsync(id, input);

    /// <summary>Submits a Draft request for approval (Draft → Pending).</summary>
    [HttpPost("{id:guid}/submit")]
    public virtual Task<ConcessionRequestDto> SubmitAsync(Guid id)
        => _appService.SubmitAsync(id);

    /// <summary>Approves a Pending request (Pending → Approved).</summary>
    [HttpPost("{id:guid}/approve")]
    public virtual Task<ConcessionRequestDto> ApproveAsync(Guid id)
        => _appService.ApproveAsync(id);

    /// <summary>Rejects a Pending request (Pending → Rejected).</summary>
    [HttpPost("{id:guid}/reject")]
    public virtual Task<ConcessionRequestDto> RejectAsync(Guid id, [FromBody] RejectConcessionRequestDto input)
        => _appService.RejectAsync(id, input);

    /// <summary>
    /// Step 4 — Value Entry (Save &amp; Continue). Full replacement operation.
    /// Persists discount amounts or percentages per fee head, grouped by concession type.
    /// The server computes and stores <c>discountAmount</c> and <c>netAmount</c> for each
    /// fee head based on the submitted <c>basedOn</c> mode and <c>amount</c>.
    /// All concession types from Step 3 must be included in a single call.
    /// </summary>
    [HttpPut("{concessionRequestId:guid}/value-entry")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public virtual Task SaveValueEntryAsync(
        Guid concessionRequestId,
        [FromBody] ValueEntryDto input)
        => _appService.SaveValueEntryAsync(concessionRequestId, input);

    /// <summary>
    /// Step 4 (Edit / Resume) — Returns the previously saved discount values
    /// (basedOn, amount, netAmount) per concession type per fee head.
    /// Pre-populates the value-entry inputs when resuming or editing a Draft request.
    /// Fee head names and base amounts are loaded separately by the UI.
    /// </summary>
    [HttpGet("{concessionRequestId:guid}/value-entries")]
    [ProducesResponseType(typeof(ApiDataResponseDto<ConcessionRequestValueEntriesDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public virtual Task<ApiDataResponseDto<ConcessionRequestValueEntriesDto>> GetValueEntriesAsync(
        Guid concessionRequestId)
        => _appService.GetValueEntriesAsync(concessionRequestId);

    /// <summary>
    /// Step 3 (Edit / Resume) — Returns the previously saved fee-head selections for every
    /// concession type in the student's request batch.
    /// Each entry carries the concession-type metadata needed to label the UI checkboxes,
    /// and an empty <c>feeHeadIds</c> list for types that have not yet had fee heads saved.
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
    /// The <paramref name="concessionRequestId"/> must identify a Draft request owned
    /// by the student whose concession types are specified in the body.
    /// </summary>
    [HttpPut("{concessionRequestId:guid}/feeHeadsData")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public virtual Task SaveFeeHeadsDataAsync(
        Guid concessionRequestId,
        [FromBody] SaveFeeHeadsDataDto input)
        => _appService.SaveFeeHeadsDataAsync(concessionRequestId, input);

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
    /// All concession types from Step 3 must be included in a single call.
    /// </summary>
    [HttpPut("{concessionRequestId:guid}/document-upload")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public virtual Task SaveDocumentUploadAsync(
        Guid concessionRequestId,
        [FromBody] UploadDocumentsDto input)
        => _appService.SaveDocumentUploadAsync(concessionRequestId, input);

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

    /// <summary>Deletes a Draft ConcessionRequest.</summary>
    [HttpDelete("{id:guid}")]
    public virtual Task DeleteAsync(Guid id)
        => _appService.DeleteAsync(id);
}
