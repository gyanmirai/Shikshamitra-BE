using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Optional message left by the approver when the request is approved.
    /// </summary>
    public string? ApproverMessage { get; private set; }

    public virtual ICollection<ConcessionFeeHeadMapping> ConcessionFeeHeadMappings { get; private set; }
        = new List<ConcessionFeeHeadMapping>();

    public virtual ICollection<ConcessionRequestDocument> ConcessionRequestDocuments { get; private set; }
        = new List<ConcessionRequestDocument>();

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
    /// Approves a pending request with an optional approver message.
    /// </summary>
    /// <param name="approverMessage">
    /// Free-text comment from the approver (e.g. conditions or notes).
    /// Trimmed and stored; <c>null</c> or whitespace clears any previously saved message.
    /// </param>
    public void Approve(string? approverMessage = null)
    {
        if (Status != ConcessionRequestStatus.Pending)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotApproveNonPending)
                .WithData("Status", Status.ToString());
        }

        Status          = ConcessionRequestStatus.Approved;
        ApproverMessage = string.IsNullOrWhiteSpace(approverMessage)
            ? null
            : approverMessage.Trim();
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

    /// <summary>
    /// Step 4 — persists discount values for every fee head of this request.
    ///
    /// <para>
    /// Invariants enforced here:
    /// <list type="bullet">
    ///   <item><description>Request must be in <b>Draft</b> status.</description></item>
    ///   <item><description>Every existing fee-head mapping must have a corresponding entry
    ///   in <paramref name="values"/> (completeness check).</description></item>
    ///   <item><description>No entry in <paramref name="values"/> may reference a fee head
    ///   that is not already mapped to this request (prevents phantom entries).</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="values">
    /// One <see cref="FeeHeadValueEntryInput"/> per mapped fee head.  The
    /// <see cref="FeeHeadValueEntryInput.AnnualBaseAmount"/> is the pre-concession annual fee
    /// amount sourced from the student's fee structure by the caller.
    /// </param>
    public void ApplyValueEntry(IReadOnlyList<FeeHeadValueEntryInput> values)
    {
        if (Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
                .WithData("RequestId", Id)
                .WithData("Status", Status.ToString());
        }

        var valueByFeeHead = values.ToDictionary(v => v.FeeHeadId);

        // Guard: every existing mapping must have a value entry
        foreach (var mapping in ConcessionFeeHeadMappings)
        {
            if (!valueByFeeHead.ContainsKey(mapping.FeeHeadId))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.FeeHeadMissingFromValueEntry)
                    .WithData("FeeHeadId", mapping.FeeHeadId)
                    .WithData("ConcessionTypeId", ConcessionTypeId)
                    .WithData("ConcessionRequestId", Id);
            }
        }

        // Guard: no entry may reference an unmapped fee head
        var mappedFeeHeadIds = ConcessionFeeHeadMappings.Select(m => m.FeeHeadId).ToHashSet();
        foreach (var entry in values)
        {
            if (!mappedFeeHeadIds.Contains(entry.FeeHeadId))
            {
                throw new BusinessException(ConcessionRequestErrorCodes.FeeHeadNotMappedToRequest)
                    .WithData("FeeHeadId", entry.FeeHeadId)
                    .WithData("ConcessionTypeId", ConcessionTypeId)
                    .WithData("ConcessionRequestId", Id);
            }
        }

        // Apply each update — ConcessionFeeHeadMapping.Update() recomputes derived amounts
        foreach (var mapping in ConcessionFeeHeadMappings)
        {
            var entry = valueByFeeHead[mapping.FeeHeadId];
            mapping.Update(
                entry.AnnualBaseAmount,
                entry.UserSpecifiedAmount,
                entry.UserSpecifiedAmountType);
        }
    }

    /// <summary>
    /// Step 6 — Saves an optional free-text description against this request.
    /// Only allowed while the request is in <b>Draft</b> status.
    /// Passing <c>null</c> or an empty string clears any previously saved description.
    /// </summary>
    public void SetDescription(string? description)
    {
        if (Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUpdateNonDraft)
                .WithData("RequestId", Id)
                .WithData("Status", Status.ToString());
        }

        Remarks = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>
    /// Step 5 — Full replacement of document uploads for this request.
    ///
    /// <para>
    /// All previously saved documents are discarded and replaced by the supplied
    /// <paramref name="documents"/> list. Only allowed while the request is in
    /// <b>Draft</b> status.
    /// </para>
    /// </summary>
    public void SetDocumentUploads(IEnumerable<ConcessionRequestDocument> documents)
    {
        if (Status != ConcessionRequestStatus.Draft)
        {
            throw new BusinessException(ConcessionRequestErrorCodes.CannotUploadDocumentsForNonDraft)
                .WithData("RequestId", Id)
                .WithData("Status", Status.ToString());
        }

        ConcessionRequestDocuments.Clear();
        foreach (var doc in documents)
        {
            ConcessionRequestDocuments.Add(doc);
        }
    }
}
