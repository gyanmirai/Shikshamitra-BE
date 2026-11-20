using System;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Receipts;

public partial class FeePaymentReceipt
{
	public static FeePaymentReceipt CreateExternal(Guid id, Guid tenantId, Guid metadataId, Guid yearId,
		FinanceExternalPayerIdentity payer, decimal amount, string currency, string number,
		DateOnly schoolDate, FeeReceiptKind kind, string? remarks)
	{
		FinanceMoneyPrecision.Ensure(amount, positive: true);
		FinanceFeePayerIdentity.Validate(payer.StudentId, null, payer.EmployeeId);
		if (id == Guid.Empty || tenantId == Guid.Empty || metadataId == Guid.Empty || yearId == Guid.Empty ||
			currency != "INR" || string.IsNullOrWhiteSpace(number) || schoolDate == default ||
			kind is not (FeeReceiptKind.FinalCharge or FeeReceiptKind.ProvisionalCharge))
			throw new BusinessException(ShikshaMitra.Finance.Fees.Collections.FeeCollectionErrorCodes.PaymentDetailsMismatch);
		var receipt = new FeePaymentReceipt(id)
		{
			TenantId = tenantId, FeeCollectionTransactionId = metadataId, AcademicYearId = yearId,
			StudentId = payer.StudentId, EmployeeId = payer.EmployeeId, TotalPaidAmount = amount,
			Currency = currency, ReceiptNumber = number, ReceiptDate = schoolDate, ReceiptScope = FeeReceiptScope.Charge,
			ReceiptKind = kind, Remarks = remarks, VerificationStatus = kind == FeeReceiptKind.ProvisionalCharge
				? FeeReceiptVerificationStatus.PendingVerification : FeeReceiptVerificationStatus.NotApplicable
		};
		receipt.SetAmountBreakdown(0m, amount);
		receipt.SetLateFeePaidAmount(0m);
		return receipt;
	}
}
