using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequestManager(
    IConcessionRequestRepository concessionRequestRepository,
    IGuidGenerator guidGenerator) : DomainService
{
    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates one <see cref="ConcessionRequest"/> per concession-type ID.
    /// Enforces the domain invariant: a student may not have two active
    /// (non-rejected) requests for the same concession type.
    /// </summary>
    public async Task<List<ConcessionRequest>> CreateAsync(
        Guid? tenantId,
        Guid studentId,
        IEnumerable<Guid> concessionTypeIds)
    {
        var created = new List<ConcessionRequest>();

        foreach (var typeId in concessionTypeIds)
        {
            await GuardNoDuplicateActiveRequestAsync(studentId, typeId);

            var entity = new ConcessionRequest(
                guidGenerator.Create(),
                tenantId,
                studentId,
                typeId);

            await concessionRequestRepository.InsertAsync(entity);
            created.Add(entity);
        }

        return created;
    }

    // -------------------------------------------------------------------------
    // Update (batch replace)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the student's Pending concession-type selection in full.
    ///
    /// <para>
    /// All existing <b>Pending</b> requests for the student are deleted, then a
    /// new request is created for each ID in <paramref name="concessionTypeIds"/>.
    /// The invariant "no active (non-rejected) duplicate per type" is checked
    /// before each insert — it catches types that are already <b>Approved</b>.
    /// </para>
    /// </summary>
    /// <returns>
    /// The first newly created <see cref="ConcessionRequest"/> (representative
    /// of the updated batch; callers may map it to a DTO for the response).
    /// </returns>
    public async Task<ConcessionRequest> UpdateAsync(
        Guid? tenantId,
        Guid studentId,
        IReadOnlyList<Guid> concessionTypeIds)
    {
        // 1. Delete all existing Pending requests for this student
        var pendingRequests = await GetAllPendingRequestsForStudentAsync(studentId);
        foreach (var req in pendingRequests)
        {
            await concessionRequestRepository.DeleteAsync(req.Id);
        }

        // 2. Recreate from the new list
        //    GuardNoDuplicateActiveRequestAsync protects against re-requesting
        //    an already-Approved type (non-Rejected requests that survived the
        //    delete above, e.g. Approved ones).
        ConcessionRequest? first = null;
        foreach (var typeId in concessionTypeIds)
        {
            await GuardNoDuplicateActiveRequestAsync(studentId, typeId);

            var entity = new ConcessionRequest(
                guidGenerator.Create(),
                tenantId,
                studentId,
                typeId);

            await concessionRequestRepository.InsertAsync(entity);
            first ??= entity;
        }

        return first!;
    }

    // -------------------------------------------------------------------------
    // Step 3 — Fee-head selection (full replacement per concession type)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the fee-head selection for every concession type listed in
    /// <paramref name="items"/> in a single atomic operation.
    ///
    /// <para>
    /// For each item the manager:
    /// <list type="bullet">
    ///   <item><description>Locates the student's Draft <see cref="ConcessionRequest"/> for that type.</description></item>
    ///   <item><description>Builds a new set of <see cref="ConcessionFeeHeadMapping"/> objects
    ///   initialised with zero amounts (amounts are a concern of a later step).</description></item>
    ///   <item><description>Delegates to <see cref="ConcessionRequest.SetFeeHeadMappings"/> which
    ///   enforces the Draft-only invariant and performs the in-memory swap.</description></item>
    ///   <item><description>Persists the updated aggregate.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">
    /// Thrown with <see cref="ConcessionRequestErrorCodes.ConcessionTypeMissingFromDraftBatch"/>
    /// when the input references a concession type that has no Draft request for the student.
    /// </exception>
    public async Task SaveFeeHeadsDataAsync(
        Guid? tenantId,
        Guid studentId,
        IReadOnlyList<ConcessionTypeFeeHeadsInput> items)
    {
        var concessionTypeIds = items.Select(i => i.ConcessionTypeId).ToList();

        // Single query loads all relevant Draft requests with their existing mappings
        var draftRequests = await concessionRequestRepository
            .GetDraftRequestsWithMappingsAsync(studentId, concessionTypeIds);

        // O(1) lookup: concession type → request entity
        var requestByType = draftRequests.ToDictionary(r => r.ConcessionTypeId);

        foreach (var item in items)
        {
            if (!requestByType.TryGetValue(item.ConcessionTypeId, out var request))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromDraftBatch)
                    .WithData("ConcessionTypeId", item.ConcessionTypeId)
                    .WithData("StudentId", studentId);
            }

            // Build replacement mappings — amounts are zero at this step;
            // they are set in a subsequent step.
            var mappings = item.FeeHeadIds
                .Select(feeHeadId => new ConcessionFeeHeadMapping(
                    guidGenerator.Create(),
                    tenantId,
                    request.Id,
                    feeHeadId,
                    preConcessionFeeHeadAmount: 0m,
                    userSpecifiedConcessionAmountValue: 0m,
                    userSpecifiedConcessionAmountType: AmountType.Flat))
                .ToList();

            // Domain entity enforces Draft-only invariant
            request.SetFeeHeadMappings(mappings);
            await concessionRequestRepository.UpdateAsync(request);
        }
    }

    // -------------------------------------------------------------------------
    // Step 4 — Value entry (discount amounts per fee head)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Persists discount values for every fee head in the student's Draft request batch.
    ///
    /// <para>
    /// For each concession type in <paramref name="items"/> the manager:
    /// <list type="bullet">
    ///   <item><description>Locates the corresponding Draft <see cref="ConcessionRequest"/>.</description></item>
    ///   <item><description>Delegates to <see cref="ConcessionRequest.ApplyValueEntry"/> which enforces
    ///   completeness (all mapped fee heads must appear) and updates each
    ///   <see cref="ConcessionFeeHeadMapping"/> — the entity recomputes <c>FeeHeadConcessionAmount</c>
    ///   and <c>PostConcessionFeeHeadAmount</c> internally.</description></item>
    ///   <item><description>Persists the updated aggregate.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">
    /// Thrown with <see cref="ConcessionRequestErrorCodes.ConcessionTypeMissingFromDraftBatch"/>
    /// when the payload references a concession type that has no Draft request for the student.
    /// </exception>
    public async Task SaveValueEntryAsync(
        Guid studentId,
        ConcessionRequestStatus status,
        IReadOnlyList<ConcessionTypeValueEntryInput> items)
    {
        // Single query: all requests for this batch with their mappings loaded
        var batchRequests = await concessionRequestRepository
            .GetBatchWithMappingsAsync(studentId, status);

        var requestByType = batchRequests.ToDictionary(r => r.ConcessionTypeId);

        foreach (var item in items)
        {
            if (!requestByType.TryGetValue(item.ConcessionTypeId, out var request))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromDraftBatch)
                    .WithData("ConcessionTypeId", item.ConcessionTypeId)
                    .WithData("StudentId", studentId);
            }

            // Entity enforces: status guard, fee-head completeness, no phantom entries
            request.ApplyValueEntry(item.FeeHeadValues);
            await concessionRequestRepository.UpdateAsync(request);
        }
    }

    // -------------------------------------------------------------------------
    // Step 5 — Document upload (full replacement per concession type)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the document uploads for every concession type listed in
    /// <paramref name="items"/> in a single atomic operation.
    ///
    /// <para>
    /// For each item the manager:
    /// <list type="bullet">
    ///   <item><description>Locates the student's Draft <see cref="ConcessionRequest"/> for that type.</description></item>
    ///   <item><description>Builds a new set of <see cref="ConcessionRequestDocument"/> objects
    ///   from the supplied URLs and statuses.</description></item>
    ///   <item><description>Delegates to <see cref="ConcessionRequest.SetDocumentUploads"/> which
    ///   enforces the Draft-only invariant and performs the in-memory swap.</description></item>
    ///   <item><description>Persists the updated aggregate.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public async Task SaveDocumentUploadAsync(
        Guid? tenantId,
        Guid studentId,
        ConcessionRequestStatus status,
        IReadOnlyList<ConcessionTypeDocumentUploadInput> items)
    {
        // Single query: all requests for this batch with their documents loaded
        var batchRequests = await concessionRequestRepository
            .GetBatchWithDocumentsAsync(studentId, status);

        var requestByType = batchRequests.ToDictionary(r => r.ConcessionTypeId);

        foreach (var item in items)
        {
            if (!requestByType.TryGetValue(item.ConcessionTypeId, out var request))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.ConcessionTypeMissingFromDocumentUpload)
                    .WithData("ConcessionTypeId", item.ConcessionTypeId)
                    .WithData("StudentId", studentId);
            }

            var documents = item.Documents
                .Select(d => new ConcessionRequestDocument(
                    guidGenerator.Create(),
                    tenantId,
                    request.Id,
                    d.DocumentId,
                    d.Url,
                    d.Status))
                .ToList();

            // Domain entity enforces Draft-only invariant
            request.SetDocumentUploads(documents);
            await concessionRequestRepository.UpdateAsync(request);
        }
    }

    // -------------------------------------------------------------------------
    // State transitions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Submits a Draft request for approval (Draft → Pending).
    /// </summary>
    public async Task<ConcessionRequest> SubmitAsync(ConcessionRequest request)
    {
        request.Submit();
        await concessionRequestRepository.UpdateAsync(request);
        return request;
    }

    /// <summary>
    /// Approves a Pending request (Pending → Approved).
    /// </summary>
    public async Task<ConcessionRequest> ApproveAsync(ConcessionRequest request)
    {
        request.Approve();
        await concessionRequestRepository.UpdateAsync(request);
        return request;
    }

    /// <summary>
    /// Rejects a Pending request with a mandatory reason (Pending → Rejected).
    /// </summary>
    public async Task<ConcessionRequest> RejectAsync(ConcessionRequest request, string rejectionReason)
    {
        Check.NotNullOrWhiteSpace(rejectionReason, nameof(rejectionReason));
        request.Reject(rejectionReason);
        await concessionRequestRepository.UpdateAsync(request);
        return request;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<List<ConcessionRequest>> GetAllPendingRequestsForStudentAsync(Guid studentId)
    {
        // Using a large maxResultCount is intentional: a student will never have
        // thousands of distinct concession-type requests.
        return await concessionRequestRepository.GetListAsync(
            skipCount: 0,
            maxResultCount: int.MaxValue,
            studentId: studentId,
            status: ConcessionRequestStatus.Pending);
    }

    /// <summary>
    /// Throws <see cref="ConcessionRequestErrorCodes.DuplicateActiveRequest"/> when
    /// the student already has a non-rejected request for the given concession type.
    /// This protects against re-requesting an Approved concession.
    /// </summary>
    private async Task GuardNoDuplicateActiveRequestAsync(Guid studentId, Guid concessionTypeId)
    {
        var existing = await concessionRequestRepository.FindAsync(r =>
            r.StudentId == studentId &&
            r.ConcessionTypeId == concessionTypeId &&
            r.Status != ConcessionRequestStatus.Rejected);

        if (existing is not null)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.DuplicateActiveRequest)
                .WithData("StudentId", studentId)
                .WithData("ConcessionTypeId", concessionTypeId)
                .WithData("ExistingStatus", existing.Status.ToString());
        }
    }
}

/// <summary>
/// Value object used as input for <see cref="ConcessionFeeHeadMapping"/> creation.
/// </summary>
public record ConcessionFeeHeadMappingInput(
    Guid FeeHeadId,
    decimal PreConcessionFeeHeadAmount,
    decimal UserSpecifiedConcessionAmountValue,
    AmountType UserSpecifiedConcessionAmountType);

/// <summary>
/// Domain input for Step 3: associates a concession type with its selected fee head IDs.
/// </summary>
public record ConcessionTypeFeeHeadsInput(
    Guid ConcessionTypeId,
    IReadOnlyList<Guid> FeeHeadIds);

/// <summary>
/// Domain input for Step 4: discount value for a single fee head.
/// </summary>
/// <param name="FeeHeadId">The fee head being discounted.</param>
/// <param name="AnnualBaseAmount">Pre-concession annual fee amount (sourced from the student's fee structure).</param>
/// <param name="UserSpecifiedAmount">The discount amount or percentage entered by the user.</param>
/// <param name="UserSpecifiedAmountType">Whether <paramref name="UserSpecifiedAmount"/> is a flat amount or a percentage.</param>
public record FeeHeadValueEntryInput(
    Guid FeeHeadId,
    decimal AnnualBaseAmount,
    decimal UserSpecifiedAmount,
    AmountType UserSpecifiedAmountType);

/// <summary>
/// Domain input for Step 4: groups fee-head value entries under one concession type.
/// </summary>
public record ConcessionTypeValueEntryInput(
    Guid ConcessionTypeId,
    IReadOnlyList<FeeHeadValueEntryInput> FeeHeadValues);

/// <summary>
/// Domain input for Step 5: a single document upload entry (URL + status).
/// </summary>
/// <param name="DocumentId">References <c>ConcessionTypeDocument.Id</c> (the template slot).</param>
/// <param name="Url">Presigned S3 or permanent URL of the uploaded file.</param>
/// <param name="Status">Upload state: Pending or Uploaded.</param>
public record DocumentUploadInput(
    Guid DocumentId,
    string Url,
    DocumentUploadStatus Status);

/// <summary>
/// Domain input for Step 5: groups document uploads under one concession type.
/// </summary>
public record ConcessionTypeDocumentUploadInput(
    Guid ConcessionTypeId,
    IReadOnlyList<DocumentUploadInput> Documents);
