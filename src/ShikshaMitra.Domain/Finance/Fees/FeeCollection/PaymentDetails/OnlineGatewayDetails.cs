using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Payment details for an online payment gateway transaction.</summary>
public class OnlineGatewayDetails : PaymentDetails
{
    public OnlineGatewayDetails(
        string transactionId,
        string gatewayName,
        string? orderId = null)
    {
        TransactionId = transactionId;
        GatewayName = gatewayName;
        OrderId = orderId;
    }

    public string TransactionId { get; }
    public string GatewayName { get; }
    public string? OrderId { get; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return TransactionId;
        yield return GatewayName;
        yield return OrderId ?? string.Empty;
    }
}
