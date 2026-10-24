using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>Cash settles at collection. Every non-cash method requires reconciliation approval.</summary>
public static class FeeReceiptPaymentPolicy
{
	public static bool RequiresVerification(IEnumerable<PaymentMethodEnum> methods)
		=> methods.Any(m => m != PaymentMethodEnum.Cash);

	/// <summary>Retained for older callers; cashier confirmation no longer finalizes receipts.</summary>
	public static bool RequiresBankConfirmation(IEnumerable<PaymentMethodEnum> methods) => false;

	/// <summary>The legacy confirmation flag cannot bypass verification.</summary>
	public static FeeReceiptKind DetermineFeeReceiptKind(
		IEnumerable<PaymentMethodEnum> methods,
		bool? bankReceiptConfirmed = null)
	{
		var methodList = methods.ToList();
		return methodList.Count > 0 && methodList.All(m => m == PaymentMethodEnum.Cash)
			? FeeReceiptKind.FinalFee
			: FeeReceiptKind.ProvisionalFee;
	}
}
