using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequestManager : DomainService
{
    private readonly IConcessionRequestRepository _concessionRequestRepository;

    public ConcessionRequestManager(IConcessionRequestRepository concessionRequestRepository)
    {
        _concessionRequestRepository = concessionRequestRepository;
    }

    /// <summary>
    /// Creates a new ConcessionRequest in Draft state.
    /// </summary>
    public async Task<ConcessionRequest> CreateAsync(
        Guid studentId,
        Guid concessionTypeId,
        IEnumerable<ConcessionFeeHeadMappingInput> feeHeadMappings,
        string? remarks = null)
    {
        var request = new ConcessionRequest(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            studentId,
            concessionTypeId,
            remarks);

        foreach (var m in feeHeadMappings)
        {
            request.ConcessionFeeHeadMappings.Add(new ConcessionFeeHeadMapping(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                request.Id,
                m.FeeHeadId,
                m.PreConcessionFeeHeadAmount,
                m.UserSpecifiedConcessionAmountValue,
                m.UserSpecifiedConcessionAmountType));
        }

        await _concessionRequestRepository.InsertAsync(request);
        return request;
    }

    /// <summary>
    /// Updates a Draft request (metadata + fee head mappings).
    /// </summary>
    public async Task<ConcessionRequest> UpdateAsync(
        ConcessionRequest request,
        Guid concessionTypeId,
        IEnumerable<ConcessionFeeHeadMappingInput> feeHeadMappings,
        string? remarks = null)
    {
        request.Update(concessionTypeId, remarks);

        var newMappings = new List<ConcessionFeeHeadMapping>();
        foreach (var m in feeHeadMappings)
        {
            newMappings.Add(new ConcessionFeeHeadMapping(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                request.Id,
                m.FeeHeadId,
                m.PreConcessionFeeHeadAmount,
                m.UserSpecifiedConcessionAmountValue,
                m.UserSpecifiedConcessionAmountType));
        }

        request.SetFeeHeadMappings(newMappings);

        await _concessionRequestRepository.UpdateAsync(request);
        return request;
    }

    /// <summary>
    /// Submits a Draft request for approval.
    /// </summary>
    public async Task<ConcessionRequest> SubmitAsync(ConcessionRequest request)
    {
        request.Submit();
        await _concessionRequestRepository.UpdateAsync(request);
        return request;
    }

    /// <summary>
    /// Approves a Pending request.
    /// </summary>
    public async Task<ConcessionRequest> ApproveAsync(ConcessionRequest request)
    {
        request.Approve();
        await _concessionRequestRepository.UpdateAsync(request);
        return request;
    }

    /// <summary>
    /// Rejects a Pending request with a mandatory reason.
    /// </summary>
    public async Task<ConcessionRequest> RejectAsync(ConcessionRequest request, string rejectionReason)
    {
        Check.NotNullOrWhiteSpace(rejectionReason, nameof(rejectionReason));
        request.Reject(rejectionReason);
        await _concessionRequestRepository.UpdateAsync(request);
        return request;
    }
}

/// <summary>
/// Value object used as input for ConcessionFeeHeadMapping creation.
/// </summary>
public record ConcessionFeeHeadMappingInput(
    Guid FeeHeadId,
    decimal PreConcessionFeeHeadAmount,
    decimal UserSpecifiedConcessionAmountValue,
    AmountType UserSpecifiedConcessionAmountType);

