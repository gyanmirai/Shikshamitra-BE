namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Indicates whether a receipt covers fee-head payments or charge payments.
/// A single collect event that contains both fee and charge lines will produce
/// two separate <c>FeePaymentReceipt</c> rows — one per scope.
/// </summary>
public enum FeeReceiptScope
{
    /// <summary>Receipt covers one or more fee-head payments.</summary>
    Fee = 1,

    /// <summary>Receipt covers one or more charge payments.</summary>
    Charge = 2,
}
