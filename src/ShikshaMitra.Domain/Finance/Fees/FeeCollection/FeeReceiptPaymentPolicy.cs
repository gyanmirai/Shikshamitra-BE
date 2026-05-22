using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Receipts;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Determines whether a set of payment methods requires a provisional (kachi)
/// fee receipt or a final (confirmed) fee receipt.
/// <para>
/// Schools may have different settlement policies for BankTransfer / CreditCard.
/// Adjust the static sets here when the product team confirms the rules.
/// </para>
/// </summary>
public static class FeeReceiptPaymentPolicy
{
    /// <summary>
    /// Payment methods that are settled immediately on presentation.
    /// Only cash is instantly confirmed — everything else enters the reconciliation inbox.
    /// </summary>
    private static readonly HashSet<PaymentMethodEnum> ConfirmedMethods =
    [
        PaymentMethodEnum.Cash,
    ];

    /// <summary>
    /// All non-cash payment methods require bank/instrument verification before the
    /// fee receipt is finalized. Any method in this set triggers a
    /// <see cref="FeeReceiptKind.ProvisionalFee"/> receipt so it appears in the
    /// reconciliation screen for staff approval.
    /// </summary>
    private static readonly HashSet<PaymentMethodEnum> VerificationRequiredMethods =
    [
        PaymentMethodEnum.Cheque,
        PaymentMethodEnum.DemandDraft,
        PaymentMethodEnum.BankTransfer,
        PaymentMethodEnum.CreditCard,
        PaymentMethodEnum.UPI,
        PaymentMethodEnum.OnlinePaymentGateway,
        PaymentMethodEnum.WalletPrepaidCard,
    ];

    /// <summary>
    /// Returns <c>true</c> when at least one of the supplied methods needs
    /// bank verification, meaning the fee receipt must be provisional.
    /// </summary>
    public static bool RequiresVerification(IEnumerable<PaymentMethodEnum> methods)
        => methods.Any(m => VerificationRequiredMethods.Contains(m));

    /// <summary>
    /// Determines the appropriate <see cref="FeeReceiptKind"/> for a fee receipt
    /// based on the payment methods used.
    /// </summary>
    public static FeeReceiptKind DetermineFeeReceiptKind(IEnumerable<PaymentMethodEnum> methods)
        => RequiresVerification(methods) ? FeeReceiptKind.ProvisionalFee : FeeReceiptKind.FinalFee;
}
