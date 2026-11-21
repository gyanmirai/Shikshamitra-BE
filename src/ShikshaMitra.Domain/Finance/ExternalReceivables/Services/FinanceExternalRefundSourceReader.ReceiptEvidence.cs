using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalRefundSourceReader
{
	private static void EnsureReceiptEvidence(Charge charge, FeeRecordMetadata header, FeePaymentReceipt receipt, decimal allocated)
	{
		try
		{
			FinanceExternalSettlementAmounts.EnsureMoney(header.TotalPaidFee, positive: true);
			if (header.Currency != "INR" || receipt.Currency != header.Currency || allocated != header.TotalPaidFee ||
				receipt.TotalPaidAmount != allocated || receipt.ReceiptScope != FeeReceiptScope.Charge || receipt.FeePaidAmount != 0m ||
				receipt.ChargePaidAmount != allocated || receipt.LateFeePaidAmount != 0m)
				throw InvalidReceipt();
			var payer = FinanceReceiptEvidence.ReadPayer(header);
			var settlement = FinanceReceiptEvidence.ReadSettlement(receipt, header);
			var expectedKind = charge.StudentId.HasValue ? FinanceExternalPayerKind.Student : FinanceExternalPayerKind.Employee;
			if (payer == null || payer.Kind != expectedKind || payer.PersonId != (charge.StudentId ?? charge.EmployeeId) ||
				settlement == null || settlement.OriginalAmount != charge.Amount || settlement.SubmittedAmount != allocated ||
				settlement.SettledAmount < allocated)
				throw InvalidReceipt();
		}
		catch (BusinessException error) when (error.Code != FeeRefundErrorCodes.SourceReceiptInvalid)
		{
			throw new BusinessException(FeeRefundErrorCodes.SourceReceiptInvalid,
				"The saved receipt evidence requires Finance review before refund.", innerException: error);
		}
	}

	private static BusinessException InvalidReceipt() => new(FeeRefundErrorCodes.SourceReceiptInvalid,
		"The saved receipt evidence requires Finance review before refund.");
}
