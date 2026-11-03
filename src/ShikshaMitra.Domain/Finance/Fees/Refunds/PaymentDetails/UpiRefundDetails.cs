using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Refund disbursement details when the refund is sent via UPI
/// from the school's bank account to the student/guardian's UPI handle.
/// </summary>
public sealed class UpiRefundDetails : RefundPaymentDetails
{
    /// <summary>Date on which the UPI transfer was made.</summary>
    public DateOnly RefundDate { get; private set; }

    /// <summary>UPI Transaction ID (may be filled after payment app confirms).</summary>
    public string? TransactionId { get; private set; }

    /// <summary>Descriptive label of the school bank account / UPI handle that is debited.</summary>
    public string SourceBankAccount { get; private set; } = default!;

    /// <summary>Name of the student or parent receiving the refund.</summary>
    public string BeneficiaryName { get; private set; } = default!;

    /// <summary>UPI ID of the recipient (e.g., priya@upi).</summary>
    public string UpiId { get; private set; } = default!;

    private UpiRefundDetails() { /* For ORM */ }

    public UpiRefundDetails(
        DateOnly refundDate,
        string   sourceBankAccount,
        string   beneficiaryName,
        string   upiId,
        string?  transactionId = null,
        string?  remarks       = null)
    {
        RefundDate        = refundDate;
        SourceBankAccount = sourceBankAccount;
        BeneficiaryName   = beneficiaryName;
        UpiId             = upiId;
        TransactionId     = transactionId;
        Remarks           = remarks;
    }

    // ABP equality atoms intentionally include null for absent optional values.
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return RefundDate;
        yield return SourceBankAccount;
        yield return BeneficiaryName;
        yield return UpiId;
        yield return TransactionId!;
        yield return Remarks!;
    }
}
