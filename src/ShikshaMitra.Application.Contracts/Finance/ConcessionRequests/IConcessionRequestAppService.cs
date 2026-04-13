using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

public interface IConcessionRequestAppService : IApplicationService
{
    /// <summary>
    /// Gets a single ConcessionRequest by its ID.
    /// The response includes the student's enrollment context and all sibling
    /// requests that share the same lifecycle status as the anchor.
    /// </summary>
    Task<ApiDataResponseDto<ConcessionRequestDetailDto>> GetAsync(Guid id);

    /// <summary>Returns a paged list of ConcessionRequests.</summary>
    Task<PagedResultDto<ConcessionRequestDto>> GetListAsync(GetConcessionRequestListRequestDto input);

    /// <summary>Creates a new ConcessionRequest in Draft state.</summary>
    Task CreateAsync(CreateConcessionRequestDto input);

    /// <summary>Updates a Draft ConcessionRequest (metadata + fee-head mappings).</summary>
    Task<ConcessionRequestDto> UpdateAsync(Guid id, UpdateConcessionRequestDto input);

    /// <summary>Submits a Draft request for approval (Draft → Pending).</summary>
    Task<ConcessionRequestDto> SubmitAsync(Guid id);

    /// <summary>Approves a Pending request (Pending → Approved).</summary>
    Task<ConcessionRequestDto> ApproveAsync(Guid id);

    /// <summary>Rejects a Pending request with a mandatory reason (Pending → Rejected).</summary>
    Task<ConcessionRequestDto> RejectAsync(Guid id, RejectConcessionRequestDto input);

    /// <summary>
    /// Step 4 — Persists discount values for every fee head in the student's Draft request
    /// batch. The server computes and stores <c>discountAmount</c> and <c>netAmount</c>
    /// (post-concession) for each fee head. Full replacement — all concession types and all
    /// mapped fee heads must be included in a single call.
    /// </summary>
    Task SaveValueEntryAsync(Guid concessionRequestId, ValueEntryDto input);

    /// <summary>
    /// Step 3 (Edit / Resume) — Returns the previously saved fee-head selections for every
    /// concession type in the student's request batch, together with concession-type metadata
    /// needed to label the checkboxes. Returns empty <c>feeHeadIds</c> for types that have
    /// not yet had fee heads saved.
    /// </summary>
    Task<ApiDataResponseDto<ConcessionRequestFeeHeadsDto>> GetFeeHeadsAsync(Guid concessionRequestId);

    /// <summary>
    /// Step 3 — Saves fee-head selections for all concession types in the student's
    /// Draft request batch. Full replacement: existing fee-head mappings for the
    /// listed concession types are discarded and replaced.
    /// </summary>
    Task SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataDto input);

    /// <summary>
    /// Step 4 (Edit / Resume) — Returns the previously saved discount values
    /// (basedOn, amount, netAmount) per concession type per fee head so the UI
    /// can pre-populate the value-entry inputs when resuming or editing an existing
    /// Draft request.
    /// </summary>
    Task<ApiDataResponseDto<ConcessionRequestValueEntriesDto>> GetValueEntriesAsync(Guid concessionRequestId);

    /// <summary>
    /// Step 5 — Persists document uploads for every concession type in the student's
    /// Draft request batch. Full replacement — all concession types from Step 3 must
    /// be included in a single call. Duplicate documentId entries within a type overwrite
    /// previously saved uploads.
    /// </summary>
    Task SaveDocumentUploadAsync(Guid concessionRequestId, UploadDocumentsDto input);

    /// <summary>
    /// Step 5 (Edit / Resume) — Returns all document slots per concession type (from the
    /// concession-type template), merged with previously saved upload data (URL, status,
    /// uploadedAt). Slots that have never been submitted return url=null, status=0,
    /// uploadedAt=null.
    /// </summary>
    Task<ApiDataResponseDto<ConcessionRequestDocumentsDto>> GetDocumentUploadsAsync(Guid concessionRequestId);

    /// <summary>
    /// Step 6 — Saves an optional free-text description against a Draft concession request.
    /// Passing null or an empty string clears any previously saved description.
    /// </summary>
    Task SaveDescriptionAsync(Guid concessionRequestId, SaveDescriptionDto input);

    /// <summary>Deletes a Draft ConcessionRequest.</summary>
    Task DeleteAsync(Guid id);
}

