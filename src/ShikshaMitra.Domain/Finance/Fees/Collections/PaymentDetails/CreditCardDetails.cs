using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Payment details for a credit card transaction.</summary>
public class CreditCardDetails : PaymentDetails
{
    public CreditCardDetails(
        string transactionId,
        string cardLast4,
        string bankName,
        string? cardHolderName = null)
    {
        TransactionId = transactionId;
        CardLast4 = cardLast4;
        BankName = bankName;
        CardHolderName = cardHolderName;
    }

    public string TransactionId { get; }
    public string CardLast4 { get; }
    public string BankName { get; }
    public string? CardHolderName { get; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return TransactionId;
        yield return CardLast4;
        yield return BankName;
        yield return CardHolderName!;
    }
}
