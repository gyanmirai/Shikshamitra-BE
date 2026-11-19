using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public class ClassicReceiptPdf_Tests
{
	[Theory]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptScope.Fee, "FEE RECEIPT")]
	[InlineData(FeeReceiptKind.ProvisionalFee, FeeReceiptScope.Fee, "PROVISIONAL FEE RECEIPT")]
	[InlineData(FeeReceiptKind.FinalCharge, FeeReceiptScope.Charge, "CHARGE RECEIPT")]
	[InlineData(FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge, "PROVISIONAL CHARGE RECEIPT")]
	[InlineData(FeeReceiptKind.FinalFee, FeeReceiptScope.Combined, "FEES AND CHARGES RECEIPT")]
	public async Task Uses_Classic_Table_And_Clean_Lifecycle_Title(FeeReceiptKind kind, FeeReceiptScope scope, string title)
	{
		var transactionId = Guid.NewGuid();
		var allocation = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), null, transactionId, Guid.NewGuid(), 15000);
		allocation.SetScheduleSnapshot("Tuition fee", "Term 1", new DateTime(2026, 5, 1), new DateTime(2026, 9, 30),
			new DateTime(2026, 5, 15), ShikshaMitra.Finance.Fees.Settings.PaymentTerms.ScheduleSource.Legacy);
		var allocations = Substitute.For<IReadOnlyRepository<FeeCollectionDueFeeAllocation, Guid>>();
		allocations.GetListAsync(Arg.Any<Expression<Func<FeeCollectionDueFeeAllocation, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new List<FeeCollectionDueFeeAllocation> { allocation });
		var receipt = new FeePaymentReceipt(Guid.NewGuid())
		{
			StudentId = Guid.NewGuid(), ReceiptNumber = "CLASSIC-2026-000001", ReceiptKind = kind, ReceiptScope = scope,
			FeeCollectionTransactionId = transactionId, TotalPaidAmount = 15000, ReceiptDate = new DateOnly(2026, 5, 15)
		};
		if (scope == FeeReceiptScope.Combined) receipt.SetAmountBreakdown(15000, 0);
		var student = new Student(receipt.StudentId.Value, null, "Aarav", null, "Sharma", Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), grNo: 148);
		var bytes = await new FeeReceiptPdfGenerator(allocations: allocations).GenerateAsync(receipt, student, null);
		var text = ReceiptPdfTextReader.Read(bytes);
		text.ShouldContain(title);
		foreach (var heading in new[] { "Fee head", "Installment", "Total fees", "Paid fees", "Pending fees", "GR NO.", "148", "Amount in words" })
			text.ShouldContain(heading);
		if (scope != FeeReceiptScope.Charge) text.ShouldContain("May 2026 - Sep 2026");
		text.ShouldContain("Received:");
		text.ShouldContain("15,000.00");
		text.ShouldNotContain("acknowledges");
		text.ShouldNotContain("Subject to payment verification");
		text.ShouldNotContain("Retain this receipt");
	}
}
