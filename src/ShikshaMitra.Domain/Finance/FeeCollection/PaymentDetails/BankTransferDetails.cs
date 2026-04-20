using System.Collections.Generic;

namespace ShikshaMitra.Finance.FeeCollection.PaymentDetails;

/// <summary>
/// Payment details for a Bank Transfer (NEFT/RTGS/IMPS) payment.
/// </summary>
public class BankTransferDetails : PaymentDetails
{
    public string TransactionReferenceNumber { get; private set; }
    public string BankName { get; private set; }
    public string? IfscCode { get; private set; }

    private BankTransferDetails() { /* For ORM */ }

    public BankTransferDetails(string transactionReferenceNumber, string bankName, string? ifscCode = null)
    {
        TransactionReferenceNumber = transactionReferenceNumber;
        BankName = bankName;
        IfscCode = ifscCode;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return TransactionReferenceNumber;
        yield return BankName;
        yield return IfscCode;
    }
}
