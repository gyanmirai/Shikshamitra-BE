using System;
using ShikshaMitra.Dashboard;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Application.Tests.Dashboard;

public class DashboardCollectionEvidence_Tests
{
	[Theory]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptVerificationStatus.NotApplicable, true)]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptVerificationStatus.Verified, true)]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptVerificationStatus.PendingVerification, false)]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptVerificationStatus.Rejected, false)]
	[InlineData(FeeReceiptKind.ProvisionalFee, FeeReceiptVerificationStatus.Verified, false)]
	[InlineData(FeeReceiptKind.FinalCharge, FeeReceiptVerificationStatus.NotApplicable, false)]
	public void Includes_Only_Terminal_Final_Fee_Evidence(FeeReceiptKind kind, FeeReceiptVerificationStatus status, bool included)
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid()) { ReceiptKind = kind, ReceiptScope = FeeReceiptScope.Fee, VerificationStatus = status };
		DashboardCollectionEvidence.ConfirmedReceipt.Compile()(receipt).ShouldBe(included);
	}

	[Fact]
	public void Excludes_A_Superseded_Final_Document()
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid()) { ReceiptKind = FeeReceiptKind.FinalFee, ReceiptScope = FeeReceiptScope.Fee,
			VerificationStatus = FeeReceiptVerificationStatus.Verified, LinkedFinalReceiptId = Guid.NewGuid() };
		DashboardCollectionEvidence.ConfirmedReceipt.Compile()(receipt).ShouldBeFalse();
	}

	[Theory]
	[InlineData(FeeCollectionStatus.Paid, true)]
	[InlineData(FeeCollectionStatus.Partial, true)]
	[InlineData(FeeCollectionStatus.InReconciliation, false)]
	[InlineData(FeeCollectionStatus.Reversed, false)]
	[InlineData(FeeCollectionStatus.Pending, false)]
	public void Requires_Confirmed_Parent_Transaction(FeeCollectionStatus status, bool included)
	{
		var transaction = new FeeRecordMetadata(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100, "TEST", status);
		DashboardCollectionEvidence.ConfirmedTransaction.Compile()(transaction).ShouldBe(included);
	}
}
