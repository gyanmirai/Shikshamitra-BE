namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Tracks the verification lifecycle of a <c>FeePaymentReceipt</c>.
/// Only meaningful for <see cref="FeeReceiptKind.ProvisionalFee"/> receipts.
/// </summary>
public enum FeeReceiptVerificationStatus
{
    /// <summary>
    /// Verification does not apply: used for <see cref="FeeReceiptKind.FinalFee"/> and
    /// <see cref="FeeReceiptKind.ProvisionalCharge"/> receipts.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// The instrument (cheque / DD) has been accepted but not yet cleared by the bank.
    /// </summary>
    PendingVerification = 1,

    /// <summary>
    /// The instrument has been cleared. A new <see cref="FeeReceiptKind.FinalFee"/> receipt
    /// has been issued and linked via <c>LinkedFinalReceiptId</c>.
    /// </summary>
    Verified = 2,

    /// <summary>
    /// The instrument failed (e.g. cheque bounce). Handled by the reconciliation module.
    /// </summary>
    Rejected = 3,
}
