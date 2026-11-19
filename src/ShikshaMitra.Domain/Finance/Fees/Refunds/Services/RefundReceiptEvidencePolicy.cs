using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

/// <summary>Attributes immutable collection allocations to their printable evidence.</summary>
public static class RefundReceiptEvidencePolicy
{
	public static bool IsSettled(FeePaymentReceipt receipt) =>
		receipt.ReceiptKind is FeeReceiptKind.FinalFee or FeeReceiptKind.FinalCharge ||
		receipt.ReceiptKind is FeeReceiptKind.ProvisionalFee or FeeReceiptKind.ProvisionalCharge &&
		receipt.VerificationStatus == FeeReceiptVerificationStatus.Verified;

	public static bool CoversPrincipal(FeePaymentReceipt receipt) =>
		receipt.ReceiptScope is FeeReceiptScope.Fee or FeeReceiptScope.Combined;

	public static bool CoversCharges(FeePaymentReceipt receipt) =>
		// Historical fee documents were used as charge refund evidence.
		receipt.LateFeePaidAmount.HasValue
			? receipt.ReceiptScope is FeeReceiptScope.Charge or FeeReceiptScope.Combined
			: CoversPrincipal(receipt);

	public static bool CoversLateFees(FeePaymentReceipt receipt) =>
		receipt.LateFeePaidAmount.HasValue
			? receipt.LateFeePaidAmount > 0m && receipt.ReceiptScope is FeeReceiptScope.Charge or FeeReceiptScope.Combined
			: CoversPrincipal(receipt);
}
