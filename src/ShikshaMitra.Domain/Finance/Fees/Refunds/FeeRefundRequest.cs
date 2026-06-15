using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Aggregate root representing a fee-refund request lifecycle.
/// Construction is owned by <see cref="FeeRefundManager"/>; the aggregate exposes only
/// status-transition methods and guards illegal transitions with <see cref="BusinessException"/>.
/// </summary>
public class FeeRefundRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid StudentId { get; private set; }

    public Guid AcademicYearId { get; private set; }

    public FeeRefundStatus Status { get; private set; }

    /// <summary>Human-readable voucher number (e.g., REF-2025-000001).</summary>
    public string VoucherNumber { get; private set; } = default!;

    /// <summary>Sum of all <see cref="FeeRefundLine.Amount"/> values.</summary>
    public double TotalAmount { get; private set; }

    /// <summary>Payment method chosen for the refund disbursement (e.g., "Cash", "Cheque", "UPI").</summary>
    public string? RefundMethod { get; private set; }

    /// <summary>
    /// Structured, method-specific disbursement details (cheque number, beneficiary account, UPI ID, etc.).
    /// Null only when <see cref="RefundMethod"/> is Cash or not yet set.
    /// Persisted as a <c>jsonb</c> column via a polymorphic value converter.
    /// </summary>
    public RefundPaymentDetails? RefundPaymentDetails { get; private set; }

    /// <summary>Optional school-provided reason / taxonomy for the refund.</summary>
    public string? Reason { get; private set; }

    /// <summary>Internal notes by the approver.</summary>
    public string? ApproverNotes { get; private set; }

    /// <summary>Identity of the user who approved or rejected this request.</summary>
    public Guid? ProcessedByUserId { get; private set; }

    /// <summary>UTC timestamp when the request was approved / rejected.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    private readonly List<FeeRefundLine> _lines = [];

    /// <summary>Immutable view of refund lines — mutate only through <see cref="FeeRefundManager"/>.</summary>
    public IReadOnlyCollection<FeeRefundLine> Lines => _lines.AsReadOnly();

    private FeeRefundRequest() { /* For ORM */ }

    internal FeeRefundRequest(
        Guid id,
        Guid? tenantId,
        Guid studentId,
        Guid academicYearId,
        string voucherNumber,
        double totalAmount,
        string? refundMethod,
        RefundPaymentDetails? refundPaymentDetails,
        string? reason)
        : base(id)
    {
        TenantId             = tenantId;
        StudentId            = studentId;
        AcademicYearId       = academicYearId;
        VoucherNumber        = voucherNumber;
        TotalAmount          = totalAmount;
        RefundMethod         = refundMethod;
        RefundPaymentDetails = refundPaymentDetails;
        Reason               = reason;
        Status               = FeeRefundStatus.Pending;
    }

    /// <summary>
    /// Updates the payment details on a <see cref="FeeRefundStatus.Pending"/> request.
    /// Only allowed before the request is reviewed or processed.
    /// </summary>
    internal void UpdatePaymentDetails(string? refundMethod, RefundPaymentDetails? refundPaymentDetails)
    {
        if (Status != FeeRefundStatus.Pending)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition)
                .WithData("CurrentStatus", Status)
                .WithData("Reason", "Payment details can only be updated on Pending requests.");

        RefundMethod         = refundMethod;
        RefundPaymentDetails = refundPaymentDetails;
    }

    internal void AddLine(FeeRefundLine line) => _lines.Add(line);

    /// <summary>
    /// Replaces all existing refund lines with <paramref name="newLines"/>.
    /// Only allowed while the request is in <see cref="FeeRefundStatus.Pending"/>.
    /// </summary>
    internal void ReplaceLines(List<FeeRefundLine> newLines, double newTotalAmount)
    {
        if (Status != FeeRefundStatus.Pending)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition)
                .WithData("CurrentStatus", Status)
                .WithData("Reason", "Lines can only be replaced on Pending requests.");

        _lines.Clear();
        foreach (var line in newLines)
            _lines.Add(line);

        TotalAmount = newTotalAmount;
    }

    /// <summary>Updates the optional reason field. Allowed only on Pending requests.</summary>
    internal void UpdateReason(string? reason)
    {
        if (Status != FeeRefundStatus.Pending)
            throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition)
                .WithData("CurrentStatus", Status)
                .WithData("Reason", "Reason can only be updated on Pending requests.");

        Reason = reason;
    }

    // ── Status transition methods ─────────────────────────────────────────────

    /// <summary>Move to Under Review (from Pending only).</summary>
    public FeeRefundRequest MarkUnderReview()
    {
        EnsureTransition(FeeRefundStatus.UnderReview, allowedFrom: FeeRefundStatus.Pending);
        Status = FeeRefundStatus.UnderReview;
        return this;
    }

    /// <summary>Approve this refund (from Pending or Under Review).</summary>
    public FeeRefundRequest Approve(Guid approverUserId, string? notes = null)
    {
        EnsureTransitionAny(FeeRefundStatus.Approved,
            FeeRefundStatus.Pending,
            FeeRefundStatus.UnderReview);

        Status = FeeRefundStatus.Approved;
        ProcessedByUserId = approverUserId;
        ProcessedAt = DateTimeOffset.UtcNow;
        ApproverNotes = notes;
        return this;
    }

    /// <summary>Reject this refund (from Pending or Under Review).</summary>
    public FeeRefundRequest Reject(Guid reviewerUserId, string? notes = null)
    {
        EnsureTransitionAny(FeeRefundStatus.Rejected,
            FeeRefundStatus.Pending,
            FeeRefundStatus.UnderReview);

        Status = FeeRefundStatus.Rejected;
        ProcessedByUserId = reviewerUserId;
        ProcessedAt = DateTimeOffset.UtcNow;
        ApproverNotes = notes;
        return this;
    }

    /// <summary>Cancel this refund (only before approval / rejection).</summary>
    public FeeRefundRequest Cancel()
    {
        EnsureTransitionAny(FeeRefundStatus.Cancelled,
            FeeRefundStatus.Pending,
            FeeRefundStatus.UnderReview);

        Status = FeeRefundStatus.Cancelled;
        ProcessedAt = DateTimeOffset.UtcNow;
        return this;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnsureTransition(FeeRefundStatus target, FeeRefundStatus allowedFrom)
    {
        if (Status != allowedFrom)
            ThrowIllegalTransition(target);
    }

    private void EnsureTransitionAny(FeeRefundStatus target, params FeeRefundStatus[] allowedFromAny)
    {
        foreach (var allowed in allowedFromAny)
            if (Status == allowed) return;

        ThrowIllegalTransition(target);
    }

    private void ThrowIllegalTransition(FeeRefundStatus target) =>
        throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition)
            .WithData("CurrentStatus", Status)
            .WithData("TargetStatus", target);
}
