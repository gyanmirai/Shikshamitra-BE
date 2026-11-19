using System;
using ShikshaMitra.Finance.Fees.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class ProvisionalChargeReceipt_Tests
{
	[Fact]
	public void Charge_receipt_can_be_verified_and_linked_to_its_final_receipt()
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid())
		{
			ReceiptKind = FeeReceiptKind.ProvisionalCharge,
			VerificationStatus = FeeReceiptVerificationStatus.PendingVerification,
		};
		var finalId = Guid.NewGuid();

		receipt.MarkVerified(finalId, "Cleared by bank");

		Assert.Equal(FeeReceiptVerificationStatus.Verified, receipt.VerificationStatus);
		Assert.Equal(finalId, receipt.LinkedFinalReceiptId);
	}

	[Fact]
	public void Charge_receipt_can_be_rejected()
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid())
		{
			ReceiptKind = FeeReceiptKind.ProvisionalCharge,
			VerificationStatus = FeeReceiptVerificationStatus.PendingVerification,
		};

		receipt.MarkRejected("Instrument bounced");

		Assert.Equal(FeeReceiptVerificationStatus.Rejected, receipt.VerificationStatus);
	}
}
