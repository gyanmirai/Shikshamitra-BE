namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Indicates whether a receipt covers fee-head payments or charge payments.
/// Collection policy determines whether mixed collections produce a combined document
/// or separate <c>FeePaymentReceipt</c> rows.
/// </summary>
public enum FeeReceiptScope
{
    /// <summary>Receipt covers one or more fee-head payments.</summary>
    Fee = 1,

    /// <summary>Receipt covers one or more charge payments.</summary>
    Charge = 2,

    /// <summary>One document covers fees (including late fees) and charges.</summary>
    Combined = 3,
}
