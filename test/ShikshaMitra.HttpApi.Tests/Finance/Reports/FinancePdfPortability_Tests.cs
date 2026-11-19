using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using QuestPDF.Infrastructure;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Reports;
using ShikshaMitra.Students;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinancePdfPortability_Tests
{
	[Theory]
	[InlineData("Synthetic cost report")]
	[InlineData("શાળાનો ખર્ચ અહેવાલ")]
	public async Task Report_renders_with_bundled_fonts_and_strict_validation(string title)
	{
		var model = new FinanceReportPreviewDto
		{
			ReportType = title,
			Columns = [new() { Id = "component", Label = "Component" }, new() { Id = "amount", Label = "Amount" }],
			Rows = [new Dictionary<string, string?> { ["component"] = title.StartsWith("Synthetic", StringComparison.Ordinal) ? "School ESI" : "શાળાનો ફાળો", ["amount"] = "325.00" }]
		};
		var bytes = await new FinancePdfService().GenerateReportPdfAsync(model);
		AssertPortable(bytes);
		SaveArtifact(title.StartsWith("Synthetic", StringComparison.Ordinal) ? "finance-report-latin" : "finance-report-gujarati", bytes);
	}

	[Theory]
	[InlineData(FeeReceiptKind.FinalFee)]
	[InlineData(FeeReceiptKind.ProvisionalFee)]
	[InlineData(FeeReceiptKind.ProvisionalCharge)]
	public async Task Existing_receipt_variants_remain_renderable_without_system_fonts(FeeReceiptKind kind)
	{
		var receipt = new FeePaymentReceipt(Guid.NewGuid())
		{
			ReceiptKind = kind,
			ReceiptNumber = "SYN-2026-000001",
			ReceiptDate = new DateOnly(2026, 10, 1),
			TotalPaidAmount = 325.00,
			Remarks = "Synthetic receipt font regression"
		};
		var student = new Student(Guid.NewGuid(), null, "દીપ્તિ", null, "શાહ", Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), grNo: 1234);
		var bytes = await new FeeReceiptPdfGenerator().GenerateAsync(receipt, student, null);
		AssertPortable(bytes);
		SaveArtifact("finance-receipt-" + kind, bytes);
	}

	private static void AssertPortable(byte[] bytes)
	{
		Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
		bytes.Length.ShouldBeGreaterThan(1000);
		QuestPDF.Settings.UseSystemFonts.ShouldBeFalse();
		QuestPDF.Settings.ThrowOnMissingFontFamilies.ShouldBeTrue();
		QuestPDF.Settings.ThrowOnMissingTextGlyphs.ShouldBeTrue();
	}

	private static void SaveArtifact(string name, byte[] bytes)
	{
		var output = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (string.IsNullOrWhiteSpace(output)) return;
		Directory.CreateDirectory(output);
		File.WriteAllBytes(Path.Combine(output, name + ".pdf"), bytes);
	}
}