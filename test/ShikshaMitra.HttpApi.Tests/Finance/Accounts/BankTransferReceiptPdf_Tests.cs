using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public class BankTransferReceiptPdf_Tests
{
	[Theory]
	[InlineData(FeeReceiptKind.ProvisionalFee, FeeReceiptScope.Fee)]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptScope.Fee)]
	[InlineData(FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge)]
	[InlineData(FeeReceiptKind.FinalCharge, FeeReceiptScope.Charge)]
	public async Task Printed_Receipt_Contains_Original_Transfer_Evidence(FeeReceiptKind kind, FeeReceiptScope receiptScope)
	{
		var transactionId = Guid.NewGuid();
		var studentId = Guid.NewGuid();
		var payment = PaymentMethodRecord.Create(studentId, transactionId, 100m, DateTimeOffset.UtcNow,
			PaymentMethodEnum.BankTransfer, new BankTransferDetails("N123456789876", "", transferDate: new(2026, 10, 20), accountHolderName: "Rahul Sharma"));
		payment.SetReceivingBank(Guid.NewGuid(), "Synthetic Test Bank • 1234");
		var payments = Substitute.For<IReadOnlyRepository<PaymentMethodRecord, Guid>>();
		payments.GetListAsync(Arg.Any<Expression<Func<PaymentMethodRecord, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(new List<PaymentMethodRecord> { payment }));
		var receipt = new FeePaymentReceipt(Guid.NewGuid()) { StudentId = studentId, FeeCollectionTransactionId = transactionId,
			ReceiptNumber = "TEST-TRANSFER-000001", ReceiptKind = kind, ReceiptScope = receiptScope, TotalPaidAmount = 100,
			ReceiptDate = new(2026, 10, 26) };
		var bytes = await new FeeReceiptPdfGenerator(paymentRecords: payments).GenerateAsync(receipt, null, null);
		var directory = Environment.GetEnvironmentVariable("RECEIPT_E2E_PDF_DIRECTORY");
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
			await File.WriteAllBytesAsync(Path.Combine(directory, $"bank-transfer-layout-{kind}.pdf"), bytes);
		}
		var text = ReceiptPdfTextReader.Read(bytes);
		text.ShouldContain("N123456789876");
		text.ShouldContain("20-Oct-2026");
		text.ShouldContain("Rahul Sharma");
		text.ShouldContain("Synthetic Test Bank");
	}
}
