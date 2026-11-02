using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Refund disbursement details when cash is handed to the student/guardian.
/// </summary>
public sealed class CashRefundDetails : RefundPaymentDetails
{
    /// <summary>Date on which the cash refund was given.</summary>
    public DateOnly RefundDate { get; private set; }

    /// <summary>Name of the person who received the cash on behalf of the student.</summary>
    public string ReceivedBy { get; private set; } = default!;

    private CashRefundDetails() { /* For ORM */ }

    public CashRefundDetails(DateOnly refundDate, string receivedBy, string? remarks = null)
    {
        RefundDate = refundDate;
        ReceivedBy = receivedBy;
        Remarks    = remarks;
    }

    // ABP equality atoms intentionally include null for absent optional values.
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return RefundDate;
        yield return ReceivedBy;
        yield return Remarks!;
    }
}
