using System;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;

namespace ShikshaMitra.Dashboard;

public static class DashboardCollectionEvidence
{
	public static Expression<Func<FeePaymentReceipt, bool>> ConfirmedReceipt => receipt =>
		receipt.ReceiptKind == FeeReceiptKind.FinalFee && receipt.LinkedFinalReceiptId == null &&
		(receipt.ReceiptScope == FeeReceiptScope.Fee || receipt.ReceiptScope == FeeReceiptScope.Combined) &&
		(receipt.VerificationStatus == FeeReceiptVerificationStatus.NotApplicable || receipt.VerificationStatus == FeeReceiptVerificationStatus.Verified);

	public static Expression<Func<FeeRecordMetadata, bool>> ConfirmedTransaction => transaction =>
		transaction.Status == FeeCollectionStatus.Paid || transaction.Status == FeeCollectionStatus.Partial;
}
