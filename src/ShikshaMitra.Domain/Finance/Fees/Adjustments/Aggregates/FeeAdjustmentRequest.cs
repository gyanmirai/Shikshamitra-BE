using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;

public class FeeAdjustmentRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid AcademicYearId { get; private set; }
    public Guid BoardId { get; private set; }
    public Guid EducationMediumId { get; private set; }
    public Guid StandardId { get; private set; }
    public Guid DivisionId { get; private set; }
    public FeeAdjustmentType AdjustmentType { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Remarks { get; private set; }
    public string VoucherNumber { get; private set; } = string.Empty;
    public double RequestedAmount { get; private set; }
    public double AppliedAmount { get; private set; }
    public double RemainingAmount { get; private set; }
    public FeeAdjustmentStatus Status { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? ApproverNotes { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int ApplyVersion { get; private set; }
    public string? LastIdempotencyKey { get; private set; }

    public virtual ICollection<FeeAdjustmentSourceLine> SourceLines { get; private set; } = [];
    public virtual ICollection<FeeAdjustmentTargetAllocation> TargetAllocations { get; private set; } = [];

    private FeeAdjustmentRequest() { }

    internal FeeAdjustmentRequest(
        Guid id,
        Guid? tenantId,
        Guid studentId,
        Guid academicYearId,
        Guid boardId,
        Guid educationMediumId,
        Guid standardId,
        Guid divisionId,
        FeeAdjustmentType adjustmentType,
        string reason,
        string? remarks,
        string voucherNumber,
        double requestedAmount)
        : base(id)
    {
        TenantId = tenantId;
        StudentId = studentId;
        AcademicYearId = academicYearId;
        BoardId = boardId;
        EducationMediumId = educationMediumId;
        StandardId = standardId;
        DivisionId = divisionId;
        AdjustmentType = adjustmentType;
        Reason = reason;
        Remarks = remarks;
        VoucherNumber = voucherNumber;
        RequestedAmount = requestedAmount;
        AppliedAmount = 0;
        RemainingAmount = requestedAmount;
        Status = FeeAdjustmentStatus.Pending;
        ApplyVersion = 0;
    }

    internal void SetSourceLines(IEnumerable<FeeAdjustmentSourceLine> lines)
    {
        SourceLines.Clear();
        foreach (var line in lines)
        {
            SourceLines.Add(line);
        }
    }

    public void EnsurePending()
    {
        if (Status != FeeAdjustmentStatus.Pending)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.NotPending)
                .WithData("Status", Status);
        }
    }

    public void EnsureCanApply()
    {
        if (Status is not FeeAdjustmentStatus.Pending and not FeeAdjustmentStatus.PartiallyApplied)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply)
                .WithData("Status", Status);
        }
    }

    public void EnsureApproverNotCreator(Guid approverId)
    {
        if (CreatorId.HasValue && approverId == CreatorId.Value)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.ApproverCannotBeRequestor);
        }
    }

    public void Reject(Guid reviewerId, string message)
    {
        EnsureCanReject();
        EnsureApproverNotCreator(reviewerId);

        Status = FeeAdjustmentStatus.Rejected;
        RejectionReason = message;
        ProcessedByUserId = reviewerId;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        EnsurePending();
        Status = FeeAdjustmentStatus.Cancelled;
    }

    public int RecordApplyBatch(
        Guid approverId,
        string? approvalNotes,
        IEnumerable<(Guid DueFeeId, Guid FeeHeadId, MonthEnum Month, int Year, double Amount)> allocations)
    {
        EnsureCanApply();
        EnsureApproverNotCreator(approverId);

        var allocationList = allocations.ToList();
        if (allocationList.Count == 0)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.NoAllocationLines);
        }

        var batchTotal = allocationList.Sum(a => a.Amount);
        if (batchTotal <= 0)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.NoAllocationLines);
        }

        if (batchTotal > RemainingAmount + 0.001)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.ApplyExceedsRemaining)
                .WithData("Requested", batchTotal)
                .WithData("Remaining", RemainingAmount);
        }

        ApplyVersion++;
        var batchNumber = ApplyVersion;
        var appliedOn = DateTime.UtcNow;

        foreach (var a in allocationList)
        {
            TargetAllocations.Add(new FeeAdjustmentTargetAllocation(
                Guid.NewGuid(),
                Id,
                a.DueFeeId,
                a.FeeHeadId,
                a.Month,
                a.Year,
                a.Amount,
                batchNumber,
                appliedOn));
        }

        AppliedAmount += batchTotal;
        RemainingAmount = Math.Max(0, RequestedAmount - AppliedAmount);
        ApproverNotes = approvalNotes;
        ProcessedByUserId = approverId;
        ProcessedAt = appliedOn;

        Status = RemainingAmount <= 0.001
            ? FeeAdjustmentStatus.Applied
            : FeeAdjustmentStatus.PartiallyApplied;

        return batchNumber;
    }

    public void SetIdempotencyKey(string key) => LastIdempotencyKey = key;

    public bool HasIdempotencyKey(string key) =>
        !string.IsNullOrEmpty(LastIdempotencyKey) &&
        string.Equals(LastIdempotencyKey, key, StringComparison.Ordinal);

    private void EnsureCanReject()
    {
        if (Status != FeeAdjustmentStatus.Pending)
        {
            throw new BusinessException(FeeAdjustmentErrorCodes.IllegalStatusTransition)
                .WithData("CurrentStatus", Status)
                .WithData("TargetStatus", FeeAdjustmentStatus.Rejected);
        }
    }
}
