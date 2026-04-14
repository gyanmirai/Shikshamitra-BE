using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests.Dtos;

namespace ShikshaMitra.Finance.ConcessionRequests;

/// <summary>
/// Orchestrates the six-step Draft wizard for a concession request batch.
/// Steps 3–6 all operate on a Draft batch identified by the anchor request ID.
/// </summary>
public interface IConcessionRequestWizardAppService : IApplicationService
{
	/// <summary>
	/// Step 3 (Edit / Resume) — Returns the previously saved fee-head selections for every
	/// concession type in the student's request batch. Returns empty <c>feeHeadIds</c> for
	/// types that have not yet had fee heads saved.
	/// </summary>
	Task<ApiDataResponseDto<ConcessionRequestFeeHeadsDto>> GetFeeHeadsAsync(Guid concessionRequestId);

	/// <summary>
	/// Step 3 — Fee Head data (Save &amp; Continue). Full replacement operation.
	/// Replaces the fee-head selection for each listed concession type.
	/// </summary>
	Task SaveFeeHeadsDataAsync(Guid concessionRequestId, SaveFeeHeadsDataDto input);

	/// <summary>
	/// Step 4 (Edit / Resume) — Returns the previously saved discount values
	/// (basedOn, amount, netAmount) per concession type per fee head.
	/// </summary>
	Task<ApiDataResponseDto<ConcessionRequestValueEntriesDto>> GetValueEntriesAsync(Guid concessionRequestId);

	/// <summary>
	/// Step 4 — Value Entry (Save &amp; Continue). Full replacement operation.
	/// Persists discount amounts or percentages per fee head, grouped by concession type.
	/// </summary>
	Task SaveValueEntryAsync(Guid concessionRequestId, ValueEntryDto input);

	/// <summary>
	/// Step 5 (Edit / Resume) — Returns all document template slots per concession type,
	/// each merged with previously saved upload data (URL, status, uploadedAt).
	/// </summary>
	Task<ApiDataResponseDto<ConcessionRequestDocumentsDto>> GetDocumentUploadsAsync(Guid concessionRequestId);

	/// <summary>
	/// Step 5 — Document Upload (Save &amp; Continue). Full replacement operation.
	/// Persists document URLs and upload statuses per concession type.
	/// </summary>
	Task SaveDocumentUploadAsync(Guid concessionRequestId, UploadDocumentsDto input);

	/// <summary>
	/// Step 6 — Save Description (optional). Saves a free-text description against a
	/// Draft concession request. Null or empty clears any previously saved description.
	/// </summary>
	Task SaveDescriptionAsync(Guid concessionRequestId, SaveDescriptionDto input);
}
