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
