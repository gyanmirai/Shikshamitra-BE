using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Refund disbursement details when a Demand Draft (DD) is issued to the student/guardian.
/// </summary>
public sealed class DemandDraftRefundDetails : RefundPaymentDetails
{
    /// <summary>Date on which the DD was issued.</summary>
    public DateOnly IssueDate { get; private set; }

    /// <summary>6–7 digit DD number.</summary>
    public string DdNumber { get; private set; } = default!;

    /// <summary>Name of the bank that issued the DD.</summary>
    public string BankName { get; private set; } = default!;

    /// <summary>Branch of the issuing bank.</summary>
    public string BranchName { get; private set; } = default!;

    /// <summary>Name of the student or parent in whose favour the DD is drawn.</summary>
    public string PayeeName { get; private set; } = default!;

    /// <summary>Descriptive label of the school bank account from which the DD is funded.</summary>
    public string SourceBankAccount { get; private set; } = default!;

    private DemandDraftRefundDetails() { /* For ORM */ }

    public DemandDraftRefundDetails(
        DateOnly issueDate,
        string   ddNumber,
        string   bankName,
        string   branchName,
        string   payeeName,
        string   sourceBankAccount,
        string?  remarks = null)
    {
        IssueDate         = issueDate;
        DdNumber          = ddNumber;
        BankName          = bankName;
        BranchName        = branchName;
        PayeeName         = payeeName;
        SourceBankAccount = sourceBankAccount;
        Remarks           = remarks;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return IssueDate;
        yield return DdNumber;
        yield return BankName;
        yield return BranchName;
        yield return PayeeName;
        yield return SourceBankAccount;
        yield return Remarks;
    }
}
