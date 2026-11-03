using System;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class RefundReceiptEvidencePolicy_Tests
{
	[Theory]
	[InlineData(FeeReceiptScope.Fee, null, true, true, true)]
	[InlineData(FeeReceiptScope.Fee, 0, true, false, false)]
	[InlineData(FeeReceiptScope.Charge, 10, false, true, true)]
	[InlineData(FeeReceiptScope.Charge, 0, false, true, false)]
	[InlineData(FeeReceiptScope.Charge, null, false, false, false)]
	[InlineData(FeeReceiptScope.Combined, 10, true, true, true)]
	[InlineData(FeeReceiptScope.Combined, 0, true, true, false)]
	[InlineData(FeeReceiptScope.Combined, null, true, true, true)]
	public void Each_allocation_is_refunded_from_its_own_document_with_legacy_fallback(
		FeeReceiptScope scope, int? lateFee, bool principal, bool charges, bool lateFees)
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid()) { ReceiptScope = scope };
		if (lateFee.HasValue) receipt.ExtraProperties["LateFeePaidAmount"] = (decimal)lateFee.Value;
		Assert.Equal(principal, RefundReceiptEvidencePolicy.CoversPrincipal(receipt));
		Assert.Equal(charges, RefundReceiptEvidencePolicy.CoversCharges(receipt));
		Assert.Equal(lateFees, RefundReceiptEvidencePolicy.CoversLateFees(receipt));
	}

	[Theory]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptVerificationStatus.NotApplicable, true)]
	[InlineData(FeeReceiptKind.FinalCharge, FeeReceiptVerificationStatus.NotApplicable, true)]
	[InlineData(FeeReceiptKind.ProvisionalCharge, FeeReceiptVerificationStatus.PendingVerification, false)]
	[InlineData(FeeReceiptKind.ProvisionalCharge, FeeReceiptVerificationStatus.Rejected, false)]
	[InlineData(FeeReceiptKind.ProvisionalCharge, FeeReceiptVerificationStatus.Verified, true)]
	[InlineData(FeeReceiptKind.ProvisionalFee, FeeReceiptVerificationStatus.Verified, true)]
	public void Refund_evidence_requires_a_settled_receipt(FeeReceiptKind kind,
		FeeReceiptVerificationStatus status, bool expected)
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid()) { ReceiptKind = kind, VerificationStatus = status };
		Assert.Equal(expected, RefundReceiptEvidencePolicy.IsSettled(receipt));
	}
}
