using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.Finance.Fees.FeeCollection;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>Auditable refund workflow. Approval reserves funds; settlement mutates the fee ledger.</summary>
public class FeeRefundRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid? StudentId { get; private set; }
    public Guid? FinanceFeeAccountId { get; private set; }
    public void LinkStudent(Guid studentId) => StudentId = FinanceFeePayerIdentity.LinkStudent(StudentId, studentId);
    public Guid AcademicYearId { get; private set; }
    public Guid? StudentFeeStructureId { get; private set; }
    public Guid? RefundPolicyId { get; private set; }
    public Guid? RefundPolicyVersionId { get; private set; }
    public Guid? ConcessionRequestId { get; private set; }
    public Guid? ReplacesRefundRequestId { get; private set; }
    public string? RequestIdempotencyKey { get; private set; }
    public int WorkflowVersion { get; private set; }
    public Guid? OriginalReceiptId { get; private set; }
    public string? OriginalReceiptNumber { get; private set; }
    public string Currency { get; private set; } = "INR";
    public FeeRefundStatus Status { get; private set; }
    public string VoucherNumber { get; private set; } = default!;
    public decimal TotalAmount { get; private set; }
    public decimal CalculatedAmount { get; private set; }
    public decimal ApprovedAmount { get; private set; }
    public decimal ProcessedAmount { get; private set; }
    public decimal ReversedAmount { get; private set; }
    public Guid? ExpenseCategoryId { get; private set; }
    public Guid? PayingBankAccountId { get; private set; }
    public string? ExpenseCategoryName { get; private set; }
    public string? PayingBankLabel { get; private set; }
    public void SetApprovalRouting(Guid categoryId, string name, Guid? bankId, string? bankLabel, string method)
    {
        EnsureTransitionAny(FeeRefundStatus.Approved, FeeRefundStatus.Submitted, FeeRefundStatus.UnderReview);
        ExpenseCategoryId = categoryId; ExpenseCategoryName = name;
        PayingBankAccountId = bankId; PayingBankLabel = bankLabel; RefundMethod = method;
    }
    public void SetPayoutRouting(Guid categoryId, string name, Guid bankId, string bankLabel, string method)
    {
        if (WorkflowVersion != 1 || Status != FeeRefundStatus.Approved ||
            (RefundMethod != null && RefundMethod != method))
            ThrowIllegalTransition(FeeRefundStatus.Processed);
        foreach (var settlement in _settlements) settlement.SetPaymentBank(bankId);
        ExpenseCategoryId = categoryId; ExpenseCategoryName = name;
        PayingBankAccountId = bankId; PayingBankLabel = bankLabel; RefundMethod = method;
    }
    public string? RefundMethod { get; private set; }
    public RefundPaymentDetails? RefundPaymentDetails { get; private set; }
    public string? Reason { get; private set; }
    public string? ApproverNotes { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ReversalReason { get; private set; }
    public string? PolicySnapshotJson { get; private set; }
    public string? CalculationSnapshotJson { get; private set; }
    public string? CalculationHash { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public DateTimeOffset? ReviewStartedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public Guid? ProcessedByUserId { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public Guid? ReversedByUserId { get; private set; }
    public bool LegacyFinanciallyApplied { get; private set; }

    private readonly List<FeeRefundLine> _lines = [];
    private readonly List<FeeRefundReservation> _reservations = [];
    private readonly List<FeeRefundSettlement> _settlements = [];
    public IReadOnlyCollection<FeeRefundLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<FeeRefundReservation> Reservations => _reservations.AsReadOnly();
    public IReadOnlyCollection<FeeRefundSettlement> Settlements => _settlements.AsReadOnly();

    private FeeRefundRequest() { }

    internal FeeRefundRequest(Guid id, Guid? tenantId, Guid? studentId, Guid academicYearId,
        string voucherNumber, string? refundMethod, RefundPaymentDetails? refundPaymentDetails,
        string? reason, bool createAsDraft = true, Guid? financeFeeAccountId = null) : base(id)
    {
        TenantId = tenantId;
        FinanceFeePayerIdentity.Validate(studentId, financeFeeAccountId);
        StudentId = studentId;
        FinanceFeeAccountId = financeFeeAccountId;
        AcademicYearId = academicYearId;
        VoucherNumber = Check.NotNullOrWhiteSpace(voucherNumber, nameof(voucherNumber), 50);
        RefundMethod = Normalize(refundMethod);
        RefundPaymentDetails = refundPaymentDetails;
        Reason = Normalize(reason);
        Status = createAsDraft ? FeeRefundStatus.Draft : FeeRefundStatus.Submitted;
        if (!createAsDraft) SubmittedAt = DateTimeOffset.UtcNow;
    }

    internal FeeRefundRequest(Guid id, Guid? tenantId, Guid? studentId, Guid academicYearId,
        string voucherNumber, IReadOnlyCollection<FeeRefundLine> lines, string? refundMethod,
        RefundPaymentDetails? refundPaymentDetails, string? reason, Guid? financeFeeAccountId = null)
        : this(id, tenantId, studentId, academicYearId, voucherNumber, refundMethod,
            refundPaymentDetails, reason, createAsDraft: false, financeFeeAccountId: financeFeeAccountId)
    {
        if (lines.Count == 0 || lines.Any(line => line.FeeRefundRequestId != id))
            throw new BusinessException(FeeRefundErrorCodes.RefundLineNotFound);
        _lines.AddRange(lines);
        Recalculate();
    }

    internal void PinPolicy(Guid? assignmentId, Guid policyId, Guid policyVersionId, Guid receiptId,
        string receiptNumber, string policySnapshotJson, string calculationSnapshotJson, string calculationHash)
    {
        if (RefundPolicyVersionId.HasValue || (!assignmentId.HasValue && !FinanceFeeAccountId.HasValue))
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        StudentFeeStructureId = assignmentId;
        RefundPolicyId = policyId;
        RefundPolicyVersionId = policyVersionId;
        OriginalReceiptId = receiptId;
        OriginalReceiptNumber = Check.NotNullOrWhiteSpace(receiptNumber, nameof(receiptNumber), 100);
        PolicySnapshotJson = Check.NotNullOrWhiteSpace(policySnapshotJson, nameof(policySnapshotJson));
        CalculationSnapshotJson = Check.NotNullOrWhiteSpace(calculationSnapshotJson, nameof(calculationSnapshotJson));
        CalculationHash = Check.NotNullOrWhiteSpace(calculationHash, nameof(calculationHash), 64);
    }

    internal void PinConcession(Guid assignmentId, Guid concessionRequestId, Guid receiptId,
        string receiptNumber, string calculationSnapshotJson, string calculationHash)
    {
        if (StudentFeeStructureId.HasValue || ConcessionRequestId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        StudentFeeStructureId = assignmentId;
        ConcessionRequestId = concessionRequestId;
        OriginalReceiptId = receiptId;
        OriginalReceiptNumber = Check.NotNullOrWhiteSpace(receiptNumber, nameof(receiptNumber), 100);
        CalculationSnapshotJson = Check.NotNullOrWhiteSpace(calculationSnapshotJson, nameof(calculationSnapshotJson));
        CalculationHash = Check.NotNullOrWhiteSpace(calculationHash, nameof(calculationHash), 64);
    }

    internal void UseSimpleWorkflow(Guid submittedBy, string? idempotencyKey = null, Guid? replacesRequestId = null)
    {
        if (Status != FeeRefundStatus.Draft && Status != FeeRefundStatus.Submitted)
            ThrowIllegalTransition(FeeRefundStatus.Submitted);
        if (_lines.Count == 0 || CalculatedAmount <= 0 || !OriginalReceiptId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        WorkflowVersion = 1;
        RequestIdempotencyKey = Normalize(idempotencyKey);
        ReplacesRefundRequestId = replacesRequestId;
        Status = FeeRefundStatus.Submitted;
        SubmittedByUserId = submittedBy;
        SubmittedAt = DateTimeOffset.UtcNow;
    }

    internal void UpdateEvidence(string calculationSnapshotJson, string calculationHash)
    {
        EnsureDraft();
        CalculationSnapshotJson = Check.NotNullOrWhiteSpace(calculationSnapshotJson, nameof(calculationSnapshotJson));
        CalculationHash = Check.NotNullOrWhiteSpace(calculationHash, nameof(calculationHash), 64);
    }

    internal void UpdatePaymentDetails(string? method, RefundPaymentDetails? details) { EnsureDraft(); RefundMethod = Normalize(method); RefundPaymentDetails = details; }
    internal void AddLine(FeeRefundLine line) { EnsureDraft(); _lines.Add(line); Recalculate(); }
    internal void ReplaceLines(List<FeeRefundLine> lines) { EnsureDraft(); _lines.Clear(); _lines.AddRange(lines); Recalculate(); }
    internal void UpdateReason(string? reason) { EnsureDraft(); Reason = Normalize(reason); }

    public FeeRefundRequest Submit(Guid userId)
    {
        EnsureTransition(FeeRefundStatus.Submitted, FeeRefundStatus.Draft);
        if (_lines.Count == 0 || TotalAmount <= 0 || CalculatedAmount <= 0 ||
            (!RefundPolicyVersionId.HasValue && !ConcessionRequestId.HasValue) || !OriginalReceiptId.HasValue)
            throw new BusinessException(FeeRefundErrorCodes.PolicyVersionInvalid);
        Status = FeeRefundStatus.Submitted;
        SubmittedByUserId = userId;
        SubmittedAt = DateTimeOffset.UtcNow;
        return this;
    }

    public FeeRefundRequest MarkUnderReview(Guid userId)
    {
        EnsureTransition(FeeRefundStatus.UnderReview, FeeRefundStatus.Submitted);
        Status = FeeRefundStatus.UnderReview;
        ReviewedByUserId = userId;
        ReviewStartedAt = DateTimeOffset.UtcNow;
        return this;
    }

    public FeeRefundRequest MarkUnderReview() => MarkUnderReview(Guid.Empty);

    public FeeRefundRequest Approve(Guid userId, IEnumerable<FeeRefundReservation> reservations, string? notes = null)
    {
        EnsureTransitionAny(FeeRefundStatus.Approved, FeeRefundStatus.Submitted, FeeRefundStatus.UnderReview);
        var reservationList = reservations.ToList();
        if (reservationList.Count != _lines.Count || reservationList.Any(r => r.FeeRefundRequestId != Id) ||
            reservationList.GroupBy(r => r.FeeRefundLineId).Any(g => g.Count() != 1))
            throw new BusinessException(FeeRefundErrorCodes.ReservationConflict);
        foreach (var line in _lines)
        {
            var reservation = reservationList.SingleOrDefault(r => r.FeeRefundLineId == line.Id)
                ?? throw new BusinessException(FeeRefundErrorCodes.ReservationConflict);
            if (reservation.ChargeId != line.ChargeId || reservation.DueFeeId != line.DueFeeId || reservation.SourceAllocationId != line.SourceAllocationId ||
                reservation.Amount != line.CalculatedAmount)
                throw new BusinessException(FeeRefundErrorCodes.ReservationConflict);
            line.Approve(reservation.Amount);
        }
        _reservations.Clear();
        _reservations.AddRange(reservationList);
        ApprovedAmount = _reservations.Sum(x => x.Amount);
        if (ApprovedAmount <= 0 || ApprovedAmount > TotalAmount) throw new BusinessException(FeeRefundErrorCodes.RefundExceedsPolicyAmount);
        Status = FeeRefundStatus.Approved;
        ReviewedByUserId = userId;
        ApproverNotes = Normalize(notes);
        return this;
    }

    public FeeRefundRequest Approve(Guid userId, string? notes = null)
    {
        EnsureTransitionAny(FeeRefundStatus.Approved, FeeRefundStatus.Submitted, FeeRefundStatus.UnderReview);
        foreach (var line in _lines) { line.Approve(line.CalculatedAmount); line.MarkProcessed(line.CalculatedAmount); }
        ApprovedAmount = TotalAmount;
        ProcessedAmount = TotalAmount;
        Status = FeeRefundStatus.Processed;
        ReviewedByUserId = userId;
        ProcessedByUserId = userId;
        ProcessedAt = DateTimeOffset.UtcNow;
        ApproverNotes = Normalize(notes);
        LegacyFinanciallyApplied = true;
        return this;
    }

    public FeeRefundRequest Reject(Guid userId, string notes)
    {
        if (WorkflowVersion == 1 && Status == FeeRefundStatus.Approved && _settlements.Count == 0)
        {
            // A checker may rescind an approval before any money is sent.
        }
        else EnsureTransitionAny(FeeRefundStatus.Rejected, FeeRefundStatus.Submitted, FeeRefundStatus.UnderReview);
        if (string.IsNullOrWhiteSpace(notes)) throw new ArgumentException("Rejection notes are required.", nameof(notes));
        ReleaseReservations(); Status = FeeRefundStatus.Rejected; ReviewedByUserId = userId; ProcessedAt = DateTimeOffset.UtcNow; ApproverNotes = notes.Trim(); return this;
    }

    public FeeRefundRequest Cancel(Guid userId, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(notes)) throw new ArgumentException("Cancellation reason is required.", nameof(notes));
        EnsureTransitionAny(FeeRefundStatus.Cancelled, FeeRefundStatus.Draft, FeeRefundStatus.Submitted, FeeRefundStatus.UnderReview, FeeRefundStatus.Approved, FeeRefundStatus.Failed);
        ReleaseReservations(); Status = FeeRefundStatus.Cancelled; ProcessedByUserId = userId; ProcessedAt = DateTimeOffset.UtcNow; ApproverNotes = Normalize(notes); return this;
    }

    public FeeRefundSettlement StartProcessing(Guid settlementId, Guid userId, string idempotencyKey,
        string method, decimal amount, DateTimeOffset settlementDate, string? externalReference, string? paymentDetailsJson)
    {
        var prior = _settlements.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (prior != null) return prior;
        EnsureTransitionAny(FeeRefundStatus.Processing, FeeRefundStatus.Approved, FeeRefundStatus.Failed);
        if (amount != ApprovedAmount || amount <= 0) throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
        var attempt = new FeeRefundSettlement(settlementId, Id, _settlements.Count + 1, userId, idempotencyKey, method, amount, settlementDate, externalReference, paymentDetailsJson);
        _settlements.Add(attempt); Status = FeeRefundStatus.Processing; FailureReason = null; return attempt;
    }

    public FeeRefundSettlement RecordPayout(Guid settlementId, Guid userId, string idempotencyKey,
        string method, decimal amount, DateTimeOffset date, string? reference, string? details,
        Guid? bankAccountId, string evidenceReference)
    {
        if (WorkflowVersion != 1 || Status != FeeRefundStatus.Approved)
            ThrowIllegalTransition(FeeRefundStatus.Approved);
        var prior = _settlements.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey);
        if (prior != null) return prior;
        if (_settlements.Count != 0 || amount != ApprovedAmount || amount <= 0 ||
            (RefundMethod != null && !string.Equals(method, RefundMethod, StringComparison.OrdinalIgnoreCase)) ||
            (RefundMethod == null && !ConcessionRequestId.HasValue))
            throw new BusinessException(FeeRefundErrorCodes.InsufficientRefundBalance);
        if (RefundMethod == null) RefundMethod = method;
        var attempt = new FeeRefundSettlement(settlementId, Id, 1, userId, idempotencyKey,
            method, amount, date, reference, details, bankAccountId, evidenceReference);
        _settlements.Add(attempt);
        return attempt;
    }

    public void ConfirmPayout(Guid settlementId, Guid userId, string key, string evidenceReference)
    {
        if (WorkflowVersion != 1 || Status != FeeRefundStatus.Approved)
            ThrowIllegalTransition(FeeRefundStatus.Processed);
        var settlement = _settlements.SingleOrDefault(x => x.Id == settlementId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        settlement.Confirm(userId, key, evidenceReference);
        foreach (var reservation in _reservations.Where(x => x.Status == FeeRefundReservationStatus.Active)) reservation.Consume();
        foreach (var line in _lines) line.MarkProcessed(line.ApprovedAmount);
        ProcessedAmount = settlement.Amount;
        Status = FeeRefundStatus.Processed;
        ProcessedByUserId = userId;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void FailPayout(Guid settlementId, Guid userId, string key, string reason, string evidenceReference)
    {
        if (WorkflowVersion != 1 || Status != FeeRefundStatus.Approved)
            ThrowIllegalTransition(FeeRefundStatus.Failed);
        var settlement = _settlements.SingleOrDefault(x => x.Id == settlementId)
            ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        settlement.Fail(userId, key, reason, evidenceReference);
        ReleaseReservations();
        Status = FeeRefundStatus.Failed;
        FailureReason = reason.Trim();
    }

    public void RecordReturnedPayout(Guid settlementId, Guid userId, string reason)
    {
        if (WorkflowVersion != 1 || Status != FeeRefundStatus.Processed || RefundMethod == "Cash")
            ThrowIllegalTransition(FeeRefundStatus.Failed);
        Reverse(settlementId, userId, reason);
        Status = FeeRefundStatus.Failed;
        FailureReason = reason.Trim();
    }

    public void CompleteSettlement(Guid settlementId, Guid userId)
    {
        if (Status != FeeRefundStatus.Processing) ThrowIllegalTransition(FeeRefundStatus.Processed);
        var settlement = _settlements.SingleOrDefault(x => x.Id == settlementId) ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        settlement.MarkProcessed();
        foreach (var reservation in _reservations.Where(x => x.Status == FeeRefundReservationStatus.Active)) reservation.Consume();
        foreach (var line in _lines) line.MarkProcessed(line.ApprovedAmount);
        ProcessedAmount = settlement.Amount; Status = FeeRefundStatus.Processed; ProcessedByUserId = userId; ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void FailSettlement(Guid settlementId, string reason)
    {
        if (Status != FeeRefundStatus.Processing) ThrowIllegalTransition(FeeRefundStatus.Failed);
        var settlement = _settlements.SingleOrDefault(x => x.Id == settlementId) ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        settlement.MarkFailed(reason); Status = FeeRefundStatus.Failed; FailureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000);
    }

    public void Reverse(Guid settlementId, Guid userId, string reason)
    {
        EnsureTransition(FeeRefundStatus.Reversed, FeeRefundStatus.Processed);
        var settlement = _settlements.SingleOrDefault(x => x.Id == settlementId && x.Status == FeeRefundSettlementStatus.Processed) ?? throw new BusinessException(FeeRefundErrorCodes.SettlementNotFound);
        settlement.MarkReversed(reason);
        foreach (var line in _lines) line.MarkReversed(line.ProcessedAmount);
        ReversedAmount = ProcessedAmount; Status = FeeRefundStatus.Reversed; ReversedByUserId = userId; ReversedAt = DateTimeOffset.UtcNow; ReversalReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 1000);
    }

    internal void MarkLegacyFinanciallyApplied() => LegacyFinanciallyApplied = true;
    private void ReleaseReservations() { foreach (var x in _reservations.Where(x => x.Status == FeeRefundReservationStatus.Active)) x.Release(); }
    private void Recalculate()
    {
        TotalAmount = _lines.Sum(x => x.RequestedAmount);
        CalculatedAmount = _lines.Sum(x => x.CalculatedAmount);
    }
    private void EnsureDraft() { if (Status != FeeRefundStatus.Draft) ThrowIllegalTransition(FeeRefundStatus.Draft); }
    private void EnsureTransition(FeeRefundStatus target, FeeRefundStatus from) { if (Status != from) ThrowIllegalTransition(target); }
    private void EnsureTransitionAny(FeeRefundStatus target, params FeeRefundStatus[] from) { if (!from.Contains(Status)) ThrowIllegalTransition(target); }
    private void ThrowIllegalTransition(FeeRefundStatus target) => throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition).WithData("CurrentStatus", Status).WithData("TargetStatus", target);
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
