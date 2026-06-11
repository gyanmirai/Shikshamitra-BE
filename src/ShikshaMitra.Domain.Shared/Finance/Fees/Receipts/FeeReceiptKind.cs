namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Classifies a <c>FeePaymentReceipt</c> document into one of three kinds.
/// Every receipt row in the database must carry exactly one kind.
/// </summary>
public enum FeeReceiptKind
{
    /// <summary>
    /// A confirmed, final fee receipt issued when payment instruments are
    /// settled immediately (Cash, UPI, Online Gateway, Wallet/Prepaid Card,
    /// Credit Card, Bank Transfer — subject to school policy).
    /// </summary>
    FinalFee = 1,

    /// <summary>
    /// A provisional (kachi) fee receipt issued when the payment instrument
    /// requires bank verification before realisation (Cheque, Demand Draft).
    /// Becomes superseded once the reconciliation creates a <see cref="FinalFee"/> receipt.
    /// </summary>
    ProvisionalFee = 2,

    /// <summary>
    /// A provisional (kachi) charge receipt issued for charge payments.
    /// Uses a distinct template and number prefix (KCH-).
    /// Finalization rules are determined per product decision; until then it
    /// stands alone and is never promoted to a final receipt.
    /// </summary>
    ProvisionalCharge = 3,
}
