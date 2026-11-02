using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>
/// Refund disbursement details when money is transferred electronically
/// (NEFT / RTGS / IMPS) from the school's bank account to the student/guardian's account.
/// </summary>
public sealed class BankTransferRefundDetails : RefundPaymentDetails
{
    /// <summary>Date on which the transfer was initiated.</summary>
    public System.DateOnly TransferDate { get; private set; }

    /// <summary>UTR / transaction reference number (may be filled after transfer completes).</summary>
    public string? TransactionId { get; private set; }

    /// <summary>Descriptive label of the school bank account that is debited.</summary>
    public string SourceBankAccount { get; private set; } = default!;

    /// <summary>Full name of the account holder receiving the refund.</summary>
    public string BeneficiaryName { get; private set; } = default!;

    /// <summary>Beneficiary's bank name.</summary>
    public string BeneficiaryBankName { get; private set; } = default!;

    /// <summary>Beneficiary's bank account number (9–18 digits).</summary>
    public string BeneficiaryAccountNumber { get; private set; } = default!;

    /// <summary>11-character IFSC code of the beneficiary's bank branch.</summary>
    public string IfscCode { get; private set; } = default!;

    private BankTransferRefundDetails() { /* For ORM */ }

    public BankTransferRefundDetails(
        System.DateOnly transferDate,
        string          sourceBankAccount,
        string          beneficiaryName,
        string          beneficiaryBankName,
        string          beneficiaryAccountNumber,
        string          ifscCode,
        string?         transactionId = null,
        string?         remarks       = null)
    {
        TransferDate              = transferDate;
        SourceBankAccount         = sourceBankAccount;
        BeneficiaryName           = beneficiaryName;
        BeneficiaryBankName       = beneficiaryBankName;
        BeneficiaryAccountNumber  = beneficiaryAccountNumber;
        IfscCode                  = ifscCode;
        TransactionId             = transactionId;
        Remarks                   = remarks;
    }

    // ABP equality atoms intentionally include null for absent optional values.
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return TransferDate;
        yield return SourceBankAccount;
        yield return BeneficiaryName;
        yield return BeneficiaryBankName;
        yield return BeneficiaryAccountNumber;
        yield return IfscCode;
        yield return TransactionId!;
        yield return Remarks!;
    }
}
