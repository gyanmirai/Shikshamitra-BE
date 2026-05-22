using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Payment details for a credit card transaction.</summary>
public class CreditCardDetails : PaymentDetails
{
    public CreditCardDetails(
        string transactionId,
        string cardLast4,
        string bankName)
    {
        TransactionId = transactionId;
        CardLast4 = cardLast4;
        BankName = bankName;
    }

    public string TransactionId { get; }
    public string CardLast4 { get; }
    public string BankName { get; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return TransactionId;
        yield return CardLast4;
        yield return BankName;
    }
}
