using System.Collections.Generic;

using System;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Payment details for a Bank Transfer (NEFT/RTGS/IMPS) payment.
/// </summary>
public class BankTransferDetails : PaymentDetails
{
    public string TransactionReferenceNumber { get; private set; }
    public string BankName { get; private set; }
    public string? IfscCode { get; private set; }
    public DateOnly? TransferDate { get; private set; }
    public string? AccountHolderName { get; private set; }

    private BankTransferDetails() { /* For ORM */ }

    public BankTransferDetails(string transactionReferenceNumber, string bankName, string? ifscCode = null,
        DateOnly? transferDate = null, string? accountHolderName = null)
    {
        TransactionReferenceNumber = transactionReferenceNumber;
        BankName = bankName;
        IfscCode = ifscCode;
        TransferDate = transferDate;
        AccountHolderName = accountHolderName;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return TransactionReferenceNumber;
        yield return BankName;
        yield return IfscCode;
        yield return TransferDate;
        yield return AccountHolderName;
    }
}
