using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Mobile;

/// <summary>A collection, recorded refund payout, or recorded payout return. Amount is always positive.</summary>
public record MobileFinanceTransaction(string TransactionId, string Kind, DateOnly Date, DateTimeOffset OccurredAt,
	string Amount, string Direction, string Status, IReadOnlyList<string> PaymentMethods,
	IReadOnlyList<MobileFinanceReceiptReference> Receipts, Guid? PaymentId = null)
{
	public string Currency => "INR";
}
