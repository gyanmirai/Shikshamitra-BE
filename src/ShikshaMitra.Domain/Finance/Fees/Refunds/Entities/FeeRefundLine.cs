using System;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public class FeeRefundLine : Entity<Guid>
{
    public Guid FeeRefundRequestId { get; private set; }
    public Guid? ChargeId { get; private set; }
    public Guid? LateFeeDueFeeId { get; private set; }
    public Guid? DueFeeId { get; private set; }
    public Guid? SourceAllocationId { get; private set; }
    public Guid? SourceReceiptId { get; private set; }
    public Guid? FeeHeadId { get; private set; }
    public string FeeHeadName { get; private set; } = string.Empty;
    public string InstallmentLabel { get; private set; } = string.Empty;
    public decimal SettledPaidAmount { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public decimal CalculatedAmount { get; private set; }
    public decimal ApprovedAmount { get; private set; }
    public decimal ProcessedAmount { get; private set; }
    public decimal ReversedAmount { get; private set; }
    public decimal Amount => RequestedAmount;
    private FeeRefundLine() { }
    internal FeeRefundLine(Guid id, Guid feeRefundRequestId, Guid dueFeeId, double amount)
        : this(id, feeRefundRequestId, dueFeeId, null, null, Guid.Empty, string.Empty,
            string.Empty, Convert.ToDecimal(amount), Convert.ToDecimal(amount), Convert.ToDecimal(amount)) { }
    internal FeeRefundLine(Guid id, Guid requestId, Guid? dueFeeId, Guid? allocationId, Guid? receiptId,
        Guid? feeHeadId, string feeHeadName, string installmentLabel, decimal settledPaidAmount,
        decimal requestedAmount, decimal calculatedAmount, Guid? chargeId = null, Guid? lateFeeDueFeeId = null) : base(id)
    {
        if ((dueFeeId.HasValue ? 1 : 0) + (chargeId.HasValue ? 1 : 0) + (lateFeeDueFeeId.HasValue ? 1 : 0) != 1) throw new ArgumentException("A refund line needs exactly one source.");
        ChargeId = chargeId; LateFeeDueFeeId = lateFeeDueFeeId;
        if (requestedAmount <= 0 || requestedAmount > settledPaidAmount) throw new ArgumentOutOfRangeException(nameof(requestedAmount));
        // A policy cap can round one selected source's share to zero. Retain that source as evidence.
        if (calculatedAmount < 0 || calculatedAmount > requestedAmount) throw new ArgumentOutOfRangeException(nameof(calculatedAmount));
        FeeRefundRequestId = requestId; DueFeeId = dueFeeId; SourceAllocationId = allocationId; SourceReceiptId = receiptId;
        FeeHeadId = feeHeadId; FeeHeadName = feeHeadName?.Trim() ?? string.Empty; InstallmentLabel = installmentLabel?.Trim() ?? string.Empty;
        SettledPaidAmount = settledPaidAmount; RequestedAmount = requestedAmount; CalculatedAmount = calculatedAmount;
    }
    internal void Approve(decimal amount) { if (amount < 0 || amount > RequestedAmount || amount == 0 && CalculatedAmount != 0) throw new ArgumentOutOfRangeException(nameof(amount)); ApprovedAmount = amount; }
    internal void MarkProcessed(decimal amount) { if (amount != ApprovedAmount) throw new ArgumentOutOfRangeException(nameof(amount)); ProcessedAmount = amount; }
    internal void MarkReversed(decimal amount) { if (amount != ProcessedAmount) throw new ArgumentOutOfRangeException(nameof(amount)); ReversedAmount = amount; }
}
