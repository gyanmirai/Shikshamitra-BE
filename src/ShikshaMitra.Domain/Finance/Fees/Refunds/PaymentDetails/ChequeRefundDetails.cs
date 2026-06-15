using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Refund disbursement details when a cheque is issued to the student/guardian.
/// The cheque is drawn on the school's bank account (<see cref="SourceBankAccount"/>)
/// and made payable to the student or parent (<see cref="PayeeName"/>).
/// </summary>
public sealed class ChequeRefundDetails : RefundPaymentDetails
{
    /// <summary>Date printed on the cheque (cheque issue date).</summary>
    public DateOnly ChequeIssueDate { get; private set; }

    /// <summary>6-digit cheque number.</summary>
    public string ChequeNumber { get; private set; } = default!;

    /// <summary>Name of the student or parent to whom the cheque is made payable.</summary>
    public string PayeeName { get; private set; } = default!;

    /// <summary>Descriptive label of the school bank account from which the cheque is drawn (e.g., "HDFC Bank (HDFC0001234)").</summary>
    public string SourceBankAccount { get; private set; } = default!;

    private ChequeRefundDetails() { /* For ORM */ }

    public ChequeRefundDetails(
        DateOnly chequeIssueDate,
        string   chequeNumber,
        string   payeeName,
        string   sourceBankAccount,
        string?  remarks = null)
    {
        ChequeIssueDate   = chequeIssueDate;
        ChequeNumber      = chequeNumber;
        PayeeName         = payeeName;
        SourceBankAccount = sourceBankAccount;
        Remarks           = remarks;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return ChequeIssueDate;
        yield return ChequeNumber;
        yield return PayeeName;
        yield return SourceBankAccount;
        yield return Remarks;
    }
}
