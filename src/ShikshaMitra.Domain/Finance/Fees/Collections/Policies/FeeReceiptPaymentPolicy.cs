using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Determines the appropriate <see cref="FeeReceiptKind"/> for a fee collection transaction.
/// <para>
/// Three payment-method buckets drive the logic:
/// <list type="bullet">
///   <item><b>AlwaysFinal</b>: Cash — settled instantly at the counter.</item>
///   <item><b>AlwaysProvisional</b>: Cheque, DemandDraft — require bank clearance via the
///     reconciliation module regardless of what the cashier claims.</item>
///   <item><b>CashierDecided</b>: UPI, CreditCard, DebitCard, BankTransfer, OnlinePaymentGateway,
///     WalletPrepaidCard — the cashier is asked "Has the amount been received in the bank
///     account?". A YES answer produces a final receipt; NO produces a provisional one.</item>
/// </list>
/// </para>
/// <para>
/// <b>Mixed-method rule (per spec: one payment per transaction):</b>
/// If the transaction includes any AlwaysProvisional method, the receipt is Provisional.
/// If all methods are AlwaysFinal, the receipt is Final.
/// Otherwise (any CashierDecided method), the outcome depends on
/// <paramref name="bankReceiptConfirmed"/>.
/// </para>
/// </summary>
public static class FeeReceiptPaymentPolicy
{
	/// <summary>Payment methods that are settled immediately with no further confirmation needed.</summary>
	private static readonly HashSet<PaymentMethodEnum> AlwaysFinalMethods =
	[
		PaymentMethodEnum.Cash,
	];

	/// <summary>
	/// Payment methods that always require bank/instrument clearance via the reconciliation workflow,
	/// regardless of the cashier's assertion.
	/// </summary>
	private static readonly HashSet<PaymentMethodEnum> AlwaysProvisionalMethods =
	[
		PaymentMethodEnum.Cheque,
		PaymentMethodEnum.DemandDraft,
	];

	/// <summary>
	/// Methods for which the cashier is asked whether the amount has landed in the bank.
	/// A confirmed answer (<c>bankReceiptConfirmed = true</c>) produces a FinalFee receipt;
	/// an unconfirmed answer (<c>bankReceiptConfirmed = false</c>) routes to the reconciliation queue.
	/// </summary>
	private static readonly HashSet<PaymentMethodEnum> CashierDecidedMethods =
	[
		PaymentMethodEnum.UPI,
		PaymentMethodEnum.CreditCard,
		PaymentMethodEnum.DebitCard,
		PaymentMethodEnum.BankTransfer
	];

	/// <summary>
	/// Returns <c>true</c> when the transaction must go through the reconciliation
	/// inbox regardless of the cashier's claim (Cheque / DemandDraft only).
	/// </summary>
	public static bool RequiresVerification(IEnumerable<PaymentMethodEnum> methods)
		=> methods.Any(m => AlwaysProvisionalMethods.Contains(m));

	/// <summary>
	/// Returns <c>true</c> when the payment method(s) belong to the CashierDecided bucket,
	/// meaning the FE must show the "Has the amount been received in the bank?" dialog.
	/// </summary>
	public static bool RequiresBankConfirmation(IEnumerable<PaymentMethodEnum> methods)
		=> methods.Any(m => CashierDecidedMethods.Contains(m));

	/// <summary>
	/// Determines the appropriate <see cref="FeeReceiptKind"/> for a fee receipt based on
	/// the payment methods used and the optional cashier bank-confirmation answer.
	/// </summary>
	/// <param name="methods">All payment methods in this transaction.</param>
	/// <param name="bankReceiptConfirmed">
	/// Applicable only for CashierDecided methods.
	/// <c>true</c> = cashier confirmed bank receipt → Final.
	/// <c>false</c> = cashier denied / uncertain → Provisional.
	/// <c>null</c> = not asked (Cash-only or Cheque/DD-only transactions) → ignored.
	/// </param>
	public static FeeReceiptKind DetermineFeeReceiptKind(
		IEnumerable<PaymentMethodEnum> methods,
		bool? bankReceiptConfirmed = null)
	{
		var methodList = methods.ToList();

		// AlwaysProvisional wins over everything else
		if (methodList.Any(m => AlwaysProvisionalMethods.Contains(m)))
			return FeeReceiptKind.ProvisionalFee;

		// All methods are Cash → instant final receipt
		if (methodList.All(m => AlwaysFinalMethods.Contains(m)))
			return FeeReceiptKind.FinalFee;

		// At least one CashierDecided method: honour the cashier's answer
		// Defensive default: treat null as "not confirmed" → provisional
		return bankReceiptConfirmed == true
			? FeeReceiptKind.FinalFee
			: FeeReceiptKind.ProvisionalFee;
	}
}
