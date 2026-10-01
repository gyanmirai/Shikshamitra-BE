using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>An immutable refund subledger posting tied to the source payout.</summary>
public class FeeRefundAccountingEntry : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid RefundRequestId { get; private set; }
    public Guid SettlementId { get; private set; }
    public Guid? OriginalReceiptId { get; private set; }
    public Guid? BankAccountId { get; private set; }
    public string Method { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsCorrection { get; private set; }
    public Guid? CorrectsEntryId { get; private set; }
    public string FeeHeadAmountsJson { get; private set; } = "[]";
    public string EvidenceReference { get; private set; } = string.Empty;
    public Guid PostedByUserId { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }

    protected FeeRefundAccountingEntry() { }

    public FeeRefundAccountingEntry(Guid id, Guid? tenantId, Guid refundRequestId,
        Guid settlementId, Guid? originalReceiptId, Guid? bankAccountId,
        string method, string? externalReference, decimal amount, bool isCorrection,
        Guid? correctsEntryId, string feeHeadAmountsJson, string evidenceReference,
        Guid postedByUserId) : base(id)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount ||
            string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(evidenceReference) ||
            string.IsNullOrWhiteSpace(feeHeadAmountsJson) ||
            isCorrection != correctsEntryId.HasValue)
            throw new ArgumentException("A refund posting needs a valid amount, method, and evidence.");
        TenantId = tenantId;
        RefundRequestId = refundRequestId;
        SettlementId = settlementId;
        OriginalReceiptId = originalReceiptId;
        BankAccountId = bankAccountId;
        Method = method;
        ExternalReference = externalReference;
        Amount = amount;
        IsCorrection = isCorrection;
        CorrectsEntryId = correctsEntryId;
        FeeHeadAmountsJson = feeHeadAmountsJson;
        EvidenceReference = evidenceReference;
        PostedByUserId = postedByUserId;
        PostedAt = DateTimeOffset.UtcNow;
    }
}
