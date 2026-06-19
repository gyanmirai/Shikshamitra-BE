namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Classifies a <c>FeePaymentReceipt</c> document into one of four kinds.
/// Every receipt row in the database must carry exactly one kind.
/// </summary>
public enum FeeReceiptKind
{
    /// <summary>
    /// A confirmed, final fee receipt issued when payment instruments are
    /// settled immediately (Cash, UPI, Online Gateway, Credit Card, Bank Transfer).
    /// </summary>
    FinalFee = 1,

    /// <summary>
    /// A provisional (kachi) fee receipt issued when the instrument requires bank
    /// verification (Cheque, Demand Draft). Promoted to <see cref="FinalFee"/> on approval.
    /// </summary>
    ProvisionalFee = 2,

    /// <summary>
    /// A provisional (kachi) charge receipt issued when a charge is paid via an
    /// instrument requiring verification (Cheque, Demand Draft). Promoted to
    /// <see cref="FinalCharge"/> on approval.
    /// </summary>
    ProvisionalCharge = 3,

    /// <summary>
    /// A confirmed, final charge receipt created when a <see cref="ProvisionalCharge"/>
    /// receipt is approved through the reconciliation workflow (FCH-{yyyy}-{nnnnnn}).
    /// </summary>
    FinalCharge = 4,
}
