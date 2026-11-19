using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public class ReceiptDocumentModel_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Historical_Receipts_Keep_Their_Original_Organization_Logo_Context(bool hasSavedOrganization)
	{
		var studentId = Guid.NewGuid(); var yearId = Guid.NewGuid(); var transactionId = Guid.NewGuid();
		var originalOrganization = Guid.NewGuid();
		var transaction = new FeeRecordMetadata(transactionId, null, studentId, yearId, 100, "REC", FeeCollectionStatus.Paid);
		if (hasSavedOrganization) transaction.SetProperty("ReceiptOrganizationId", originalOrganization);
		ReceiptCollectionSnapshot.Capture(transaction, [new("Tuition fee", "May 2026", 100, 100, 0, 100)]);
		var transactions = Substitute.For<IReadOnlyRepository<FeeRecordMetadata, Guid>>();
		transactions.FindAsync(transactionId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(transaction);
		var identity = Substitute.For<IReceiptSchoolIdentityReader>();
		identity.ReadAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
			.Returns(new ReceiptSchoolIdentity("School"));
		var receipt = new FeePaymentReceipt(Guid.NewGuid()) { ReceiptNumber = "HISTORY-1", ReceiptScope = FeeReceiptScope.Fee,
			ReceiptKind = FeeReceiptKind.FinalFee, AcademicYearId = yearId, BoardId = Guid.NewGuid(), EducationMediumId = Guid.NewGuid(),
			FeeCollectionTransactionId = transactionId, TotalPaidAmount = 100, ReceiptDate = new(2026, 5, 15) };
		var student = new Student(studentId, null, "Aarav", null, "Sharma", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		await new FeeReceiptPdfGenerator(transactions: transactions, schoolIdentity: identity).GenerateAsync(receipt, student, null);
		await identity.Received(1).ReadAsync(receipt.BoardId, receipt.EducationMediumId, yearId,
			hasSavedOrganization ? originalOrganization : null, Arg.Any<CancellationToken>());
	}
	[Fact]
	public void Refund_Uses_The_Students_Alphanumeric_GR_Number()
	{
		var refund = Refund(); refund.StudentGrNo = null;
		var model = RefundReceiptDocumentFactory.Create(refund, new("School"), "GR-2026-A148");
		model.StudentFields.Single(field => field.Label == "GR No.").Value.ShouldBe("GR-2026-A148");
	}
	[Fact]
	public void Rounded_Final_Payment_Keeps_The_Canonical_Zero_Pending_Balance()
	{
		var due = new DueFee(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), MonthEnum.May,
			2026, false, 100.50m, new DateTime(2026, 5, 15));
		var line = ReceiptCollectionSnapshot.FeeLine(due, "Tuition fee", 101m);
		line.TotalFees.ShouldBe(100.50m);
		line.PaidFees.ShouldBe(101m);
		line.PendingFees.ShouldBe(0m);
	}

	[Fact]
	public void Collection_Snapshot_RoundTrips_And_Cannot_Be_Replaced_By_Later_Balances()
	{
		var transaction = new FeeRecordMetadata(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
			15000, "FIN-2026-000001", FeeCollectionStatus.Partial);
		ReceiptCollectionSnapshot.Capture(transaction, [new("Tuition fee", "May 2026 - Sep 2026", 25000, 15000, 10000, 15000)]);
		var organization = Guid.NewGuid();
		ReceiptCollectionSnapshot.CaptureOrganization(transaction, organization);
		ReceiptCollectionSnapshot.CaptureOrganization(transaction, Guid.NewGuid());
		ReceiptCollectionSnapshot.Capture(transaction, [new("Tuition fee", "May 2026 - Sep 2026", 25000, 25000, 0, 10000)]);
		var row = ReceiptCollectionSnapshot.Read(transaction).Single();
		row.TotalFees.ShouldBe(25000); row.PaidFees.ShouldBe(15000); row.PendingFees.ShouldBe(10000);
		ReceiptCollectionSnapshot.ReadOrganization(transaction).ShouldBe(organization);
		ReceiptCollectionSnapshot.Read(null).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("2026-05-01", "2026-09-30", "May 2026 - Sep 2026")]
	[InlineData("2026-12-01", "2027-01-31", "Dec 2026 - Jan 2027")]
	[InlineData("2026-05-01", "2026-05-31", "May 2026")]
	public void Coverage_Dates_Are_Used_Instead_Of_A_Renamable_Term_Label(string start, string end, string expected)
		=> ClassicReceiptPdfRenderer.Period(DateTime.Parse(start), DateTime.Parse(end), "Term one").ShouldBe(expected);

	[Fact]
	public async Task Fee_And_Charge_Pdfs_Use_Their_Saved_Scope_And_School_Identity()
	{
		var id = Guid.NewGuid(); var tenantId = Guid.NewGuid();
		var transaction = new FeeRecordMetadata(id, tenantId, Guid.NewGuid(), Guid.NewGuid(), 15500, "REC", FeeCollectionStatus.Partial);
		ReceiptCollectionSnapshot.Capture(transaction, [new("Tuition fee", "May 2026 - Sep 2026", 25000, 15000, 10000, 15000),
			new("Transport charge", "—", 1000, 500, 500, 500, true)]);
		var transactions = Substitute.For<IReadOnlyRepository<FeeRecordMetadata, Guid>>();
		transactions.FindAsync(id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(transaction);
		var identity = Substitute.For<IReceiptSchoolIdentityReader>();
		identity.ReadAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
			.Returns(new ReceiptSchoolIdentity("Configured School", "Sector 12, Surat, Gujarat - 395010"));
		var generator = new FeeReceiptPdfGenerator(transactions: transactions, schoolIdentity: identity);
		foreach (var scope in new[] { FeeReceiptScope.Fee, FeeReceiptScope.Charge, FeeReceiptScope.Combined })
		{
			var receipt = new FeePaymentReceipt(Guid.NewGuid()) { ReceiptNumber = "FIN-2026-000001", ReceiptScope = scope,
				ReceiptKind = FeeReceiptKind.FinalFee, FeeCollectionTransactionId = id, ReceiptDate = new(2026, 5, 15),
				AcademicYearId = transaction.AcademicYearId, TotalPaidAmount = scope == FeeReceiptScope.Fee ? 15000 : scope == FeeReceiptScope.Charge ? 500 : 15500 };
			var bytes = await generator.GenerateAsync(receipt, null, null);
			Save($"classic-{scope}-official.pdf", bytes);
			receipt.ReceiptKind = scope == FeeReceiptScope.Charge ? FeeReceiptKind.ProvisionalCharge : FeeReceiptKind.ProvisionalFee;
			Save($"classic-{scope}-provisional.pdf", await generator.GenerateAsync(receipt, null, null));
			var text = ReceiptPdfTextReader.Read(bytes);
			text.ShouldContain("Configured School"); text.ShouldContain("Sector 12, Surat");
			if (scope != FeeReceiptScope.Charge) { text.ShouldContain("Tuition fee"); text.ShouldContain("25,000.00"); text.ShouldContain("10,000.00"); }
			else text.ShouldNotContain("Tuition fee");
			if (scope != FeeReceiptScope.Fee) text.ShouldContain("Transport charge");
			else text.ShouldNotContain("Transport charge");
		}
	}

	[Theory]
	[InlineData(FeeRefundStatus.Submitted)]
	[InlineData(FeeRefundStatus.Approved)]
	[InlineData(FeeRefundStatus.Failed)]
	[InlineData(FeeRefundStatus.Rejected)]
	[InlineData(FeeRefundStatus.Cancelled)]
	[InlineData(FeeRefundStatus.Reversed)]
	public void An_Unpaid_Or_Reversed_Refund_Cannot_Produce_A_Payment_Receipt(FeeRefundStatus status)
	{
		var refund = Refund(); refund.Status = status;
		Should.Throw<BusinessException>(() => RefundReceiptDocumentFactory.Create(refund, new("School")))
			.Code.ShouldBe(FeeRefundErrorCodes.PaymentNotSettled);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Refund_Rendering_Retains_Payout_State_And_Masked_References(bool provisional)
	{
		var refund = Refund();
		if (provisional) { refund.Status = FeeRefundStatus.Processing; refund.ProcessedAmount = 0; refund.Lines[0].ProcessedAmount = 0; refund.Settlements[0].Status = FeeRefundSettlementStatus.Processing; }
		var model = RefundReceiptDocumentFactory.Create(refund, new("Configured School", "Configured address"));
		model.Amount.ShouldBe(15000); model.IsRefund.ShouldBeTrue();
		model.Lines.Single().PaidFees.ShouldBe(provisional ? 0 : 15000);
		model.Lines.Single().PendingFees.ShouldBe(provisional ? 15000 : 0);
		var bytes = ClassicReceiptPdfRenderer.Render(model);
		var text = ReceiptPdfTextReader.Read(bytes);
		text.ShouldContain(provisional ? "PROVISIONAL REFUND RECEIPT" : "REFUND RECEIPT");
		text.ShouldContain("****4821"); text.ShouldContain("GR NO."); text.ShouldContain("148");
		text.ShouldNotContain("acknowledgment"); text.ShouldNotContain("SAMPLE");
		Save(provisional ? "classic-refund-provisional.pdf" : "classic-refund-official.pdf", bytes);
	}

	[Fact]
	public void Long_Student_And_School_Names_And_Many_Lines_Render_Without_Losing_Totals()
	{
		var model = new ReceiptDocumentModel { School = new("Long School Name for International Education and Learning", "A long configured school address, Surat, Gujarat, India - 395010"),
			Title = "FEE RECEIPT", Number = "FIN-2026-000001", Date = new(2026, 5, 15), AcademicYear = "2026 - 2027",
			StudentFields = [new("Student name", "Alexandra Prerna Long Student Surname for Layout Verification"), new("GR No.", "148"), new("Class / section", "VII - B")],
			PaymentFields = [new("Payment mode", "Bank transfer"), new("Bank", "A long bank branch and account label • 4821"), new("Transaction / UTR no.", "HDFC26051500482")],
			Lines = Enumerable.Range(1, 40).Select(index => new ReceiptDocumentLine($"Tuition fee {index}", "May 2026 - Sep 2026", 1000, 750, 250, 750)).ToList(), Amount = 30000 };
		var bytes = ClassicReceiptPdfRenderer.Render(model);
		var text = ReceiptPdfTextReader.Read(bytes);
		text.ShouldContain("Tuition fee 40"); text.ShouldContain("40,000.00"); text.ShouldContain("30,000.00");
		Save("classic-multipage.pdf", bytes);
	}

	private static FeeRefundRequestDto Refund() => new() { Id = Guid.NewGuid(), StudentName = "Aarav Sharma", StudentGrNo = 148,
		VoucherNumber = "REF-2026-000001", Status = FeeRefundStatus.Processed, ApprovedAmount = 15000, ProcessedAmount = 15000,
		AcademicYearName = "2026 - 2027", CreationTime = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero), RefundMethod = "Bank transfer",
		PayingBankLabel = "School Bank • 4821", OriginalReceiptNumber = "FIN-2026-000001",
		Lines = [new() { FeeHeadName = "Tuition fee", InstallmentLabel = "May 2026 - Sep 2026", ApprovedAmount = 15000, ProcessedAmount = 15000 }],
		Settlements = [new() { AttemptNumber = 1, Amount = 15000, Method = "Bank transfer", Status = FeeRefundSettlementStatus.Processed,
			ExternalReference = "****4821", SettlementDate = new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero) }] };

	private static void Save(string name, byte[] bytes)
	{
		var folder = Environment.GetEnvironmentVariable("RECEIPT_E2E_PDF_DIRECTORY");
		if (string.IsNullOrWhiteSpace(folder)) return;
		Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name), bytes);
	}
}
