using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Payment details for a UPI payment.
/// </summary>
public class UpiDetails : PaymentDetails
{
    public string UpiTransactionId { get; private set; }
    public string UpiId { get; private set; }

    /// <summary>E.g. GPay, PhonePe, Paytm.</summary>
    public string? AppName { get; private set; }

    private UpiDetails() { /* For ORM */ }

    public UpiDetails(string upiTransactionId, string upiId, string? appName = null)
    {
        UpiTransactionId = upiTransactionId;
        UpiId = upiId;
        AppName = appName;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return UpiTransactionId;
        yield return UpiId;
        yield return AppName;
    }
}
