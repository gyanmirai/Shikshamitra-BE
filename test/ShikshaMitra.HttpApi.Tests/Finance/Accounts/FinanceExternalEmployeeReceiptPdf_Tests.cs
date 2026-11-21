using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

public class FinanceExternalEmployeeReceiptPdf_Tests
{
	[Theory]
	[InlineData(FeeReceiptKind.FinalCharge)]
	[InlineData(FeeReceiptKind.ProvisionalCharge)]
	public async Task Employee_Receipt_Pdf_Should_Render_The_Original_Payer_And_Exact_Paise(FeeReceiptKind kind)
	{
		var tenantId = Guid.NewGuid();
		var yearId = Guid.NewGuid();
		var employeeId = Guid.NewGuid();
		var number = kind == FeeReceiptKind.ProvisionalCharge ? "KCH-2026-000084" : "FCH-2026-000084";
		var payer = new FinanceExternalPayerIdentity(FinanceExternalPayerKind.Employee, employeeId);
		var metadata = FeeRecordMetadata.CreateExternal(Guid.NewGuid(), tenantId, payer, yearId, 40.25m,
			"INR", number, kind == FeeReceiptKind.ProvisionalCharge ? FeeCollectionStatus.InReconciliation : FeeCollectionStatus.Paid,
			"receipt-test-84", new string('a', 64), null);
		metadata.SetProperty("ReceiptPayerSnapshotV1", JsonSerializer.Serialize(new
		{
			Kind = FinanceExternalPayerKind.Employee, PersonId = employeeId, Name = "Maya José", Code = "STAFF-84"
		}));
		var pending = kind == FeeReceiptKind.ProvisionalCharge;
		var settled = pending ? 0m : 40.25m;
		var outstanding = pending ? 100.75m : 60.50m;
		metadata.SetProperty("ReceiptSettlementSnapshotV1", JsonSerializer.Serialize(new
		{
			OriginalAmount = 100.75m, SubmittedAmount = 40.25m, SettledAmount = settled,
			PendingVerificationAmount = pending ? 40.25m : 0m,
			FinancialOutstandingAmount = outstanding, CollectibleAmount = 60.50m
		}));
		ReceiptCollectionSnapshot.Capture(metadata,
			[new("LIB-ATLAS-LOSS", "Original Library assessment", 100.75m, settled, outstanding, 40.25m, true)]);
		var transactions = Substitute.For<IReadOnlyRepository<FeeRecordMetadata, Guid>>();
		transactions.FindAsync(metadata.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(metadata);
		var school = Substitute.For<IReceiptSchoolIdentityReader>();
		school.ReadAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
			.Returns(new ReceiptSchoolIdentity("Configured School", "Original school address"));
		var receipt = FeePaymentReceipt.CreateExternal(Guid.NewGuid(), tenantId, metadata.Id, yearId,
			payer, 40.25m, "INR", number, new DateOnly(2026, 11, 17), kind, null);

		var bytes = await new FeeReceiptPdfGenerator(transactions: transactions, schoolIdentity: school)
			.GenerateAsync(receipt, student: null, academicYear: null);
		var text = ReceiptPdfTextReader.Read(bytes);
		var folder = Environment.GetEnvironmentVariable("RECEIPT_E2E_PDF_DIRECTORY");
		if (!string.IsNullOrWhiteSpace(folder))
		{
			Directory.CreateDirectory(folder);
			await File.WriteAllBytesAsync(Path.Combine(folder, $"employee-{kind}-exact-paise.pdf"), bytes);
		}

		text.ShouldContain("Maya José");
		text.ShouldContain("STAFF-84");
		text.ShouldContain("EMPLOYEE DETAILS");
		text.ShouldNotContain("STUDENT DETAILS");
		text.ShouldContain(receipt.ReceiptNumber);
		text.ShouldContain("17 Nov 2026");
		text.ShouldContain("LIB-ATLAS-LOSS");
		text.ShouldContain("100.75");
		text.ShouldContain("40.25");
		text.ShouldContain("60.50");
		var normalized = Regex.Replace(text, @"\s+", " ");
		normalized.ShouldMatch(@"(?i)Submitted\s+amount\s*(?:₹\s*)?40\.25\b");
		normalized.ShouldMatch(pending
			? @"(?i)Pending\s+verification\s*(?:₹\s*)?40\.25\b"
			: @"(?i)Pending\s+verification\s*(?:₹\s*)?0\.00\b");
		normalized.ShouldMatch(pending
			? @"(?i)Settled\s+amount\s*(?:₹\s*)?0\.00\b"
			: @"(?i)Settled\s+amount\s*(?:₹\s*)?40\.25\b");
		normalized.ShouldMatch(pending
			? @"(?i)Financial\s+outstanding\s*(?:₹\s*)?100\.75\b"
			: @"(?i)Financial\s+outstanding\s*(?:₹\s*)?60\.50\b");
		normalized.ShouldMatch(@"(?i)Collectible\s+remaining\s*(?:₹\s*)?60\.50\b");
	}
}
