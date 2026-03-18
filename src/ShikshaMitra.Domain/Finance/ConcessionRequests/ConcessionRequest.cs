using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid ConcessionTypeId { get; private set; }

    public ConcessionRequestStatus Status { get; private set; }

    /// <summary>
    /// Reason provided when the request is rejected.
    /// </summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Additional remarks supplied by the requestor.
    /// </summary>
    public string? Remarks { get; private set; }

    public virtual ICollection<ConcessionFeeHeadMapping> ConcessionFeeHeadMappings { get; private set; }
        = new List<ConcessionFeeHeadMapping>();

    protected ConcessionRequest()
    {
        /* Required by ORM / deserialization */
    }

    public ConcessionRequest(
        Guid id,
        Guid? tenantId,
        Guid studentId,
        Guid concessionTypeId,
        string? remarks = null) : base(id)
    {
        TenantId = tenantId;
        StudentId = Check.NotNull(studentId, nameof(studentId));
        ConcessionTypeId = Check.NotNull(concessionTypeId, nameof(concessionTypeId));
        Status = ConcessionRequestStatus.Draft;
        Remarks = remarks;
    }

    /// <summary>
    /// Transitions the request from Draft to Pending (submitted for approval).
    /// </summary>
    public void Submit()
    {
        if (Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotSubmitNonDraft)
                .WithData("Status", Status.ToString());
        }

        if (ConcessionFeeHeadMappings.Count == 0)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.NoFeeHeadMappings);
        }

        Status = ConcessionRequestStatus.Pending;
    }

    /// <summary>
    /// Approves a pending request.
    /// </summary>
    public void Approve()
    {
        if (Status != ConcessionRequestStatus.Pending)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotApproveNonPending)
                .WithData("Status", Status.ToString());
        }

        Status = ConcessionRequestStatus.Approved;
    }

    /// <summary>
    /// Rejects a pending request with a mandatory reason.
    /// </summary>
    public void Reject(string rejectionReason)
    {
        if (Status != ConcessionRequestStatus.Pending)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotRejectNonPending)
                .WithData("Status", Status.ToString());
        }

        RejectionReason = Check.NotNullOrWhiteSpace(rejectionReason, nameof(rejectionReason));
        Status = ConcessionRequestStatus.Rejected;
    }

    /// <summary>
    /// Updates mutable fields. Only allowed for Draft requests.
    /// </summary>
    public void Update(Guid concessionTypeId, string? remarks)
    {
        if (Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
                .WithData("Status", Status.ToString());
        }

        ConcessionTypeId = Check.NotNull(concessionTypeId, nameof(concessionTypeId));
        Remarks = remarks;
    }

    /// <summary>
    /// Replaces the fee-head mappings. Only allowed for Draft requests.
    /// </summary>
    public void SetFeeHeadMappings(IEnumerable<ConcessionFeeHeadMapping> mappings)
    {
        if (Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
                .WithData("Status", Status.ToString());
        }

        ConcessionFeeHeadMappings.Clear();
        foreach (var mapping in mappings)
        {
            ConcessionFeeHeadMappings.Add(mapping);
        }
    }
}
