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
    /// A collect event that uses only these methods produces a <see cref="FeeReceiptKind.FinalFee"/> receipt.
    /// </summary>
    private static readonly HashSet<PaymentMethodEnum> ConfirmedMethods =
    [
        PaymentMethodEnum.Cash,
        PaymentMethodEnum.UPI,
        PaymentMethodEnum.OnlinePaymentGateway,
        PaymentMethodEnum.WalletPrepaidCard,
        PaymentMethodEnum.CreditCard,
        PaymentMethodEnum.BankTransfer,   // default confirmed — adjust per school if needed
    ];

    /// <summary>
    /// Payment methods that require bank verification before realization.
    /// Any method in this set triggers a <see cref="FeeReceiptKind.ProvisionalFee"/> receipt.
    /// </summary>
    private static readonly HashSet<PaymentMethodEnum> VerificationRequiredMethods =
    [
        PaymentMethodEnum.Cheque,
        PaymentMethodEnum.DemandDraft,
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
