using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Payment details for a prepaid wallet or wallet-based transaction.</summary>
public class WalletDetails : PaymentDetails
{
    public WalletDetails(
        string transactionId,
        string walletProvider,
        string? mobileNumber = null)
    {
        TransactionId = transactionId;
        WalletProvider = walletProvider;
        MobileNumber = mobileNumber;
    }

    public string TransactionId { get; }
    public string WalletProvider { get; }
    public string? MobileNumber { get; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return TransactionId;
        yield return WalletProvider;
        yield return MobileNumber ?? string.Empty;
    }
}
