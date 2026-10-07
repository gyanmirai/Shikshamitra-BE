using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipPdfLayout_Tests
{
	[Theory]
	[InlineData("10526.55", "Rs. 10,526.55")]
	[InlineData("272.73", "Rs. 272.73")]
	[InlineData("0.01", "Rs. 0.01")]
	[InlineData("-0.01", "Rs. -0.01")]
	[InlineData("10000", "Rs. 10,000")]
	public void Pdf_currency_preserves_recorded_paise_instead_of_rounding_each_line_up(string recorded, string expected)
	{
		PayslipPdfFormatting.FormatCurrency(decimal.Parse(recorded, System.Globalization.CultureInfo.InvariantCulture))
			.ShouldBe(expected);
	}

	[Fact]
	public void Paid_settlement_pdf_keeps_component_paise_and_matching_net_pay_words()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var dto = new SalarySlipDto
		{
			EmployeeName = "Synthetic Settlement Employee", EmployeeCode = "SYN-PAISE", MonthYear = "2026-11",
			ProcessedDate = DateTime.UtcNow, Status = SalarySlipStatus.Finalized,
			FullGrossSalary = 10000m, TotalEarning = 10526.55m, NetSalary = 10526.55m,
			NetPayInWords = "Ten Thousand Five Hundred Twenty Seven Rupees only",
			Earnings = [
				new() { Name = "Basic Salary", Amount = 6000m, FullMonthAmount = 6000m },
				new() { Name = "Special Allowance", Amount = 4000m, FullMonthAmount = 4000m },
				new() { Name = "Basic Salary (2026-10 correction)", Amount = 272.73m, IsTemp = true },
				new() { Name = "Special Allowance (2026-10 correction)", Amount = 181.82m, IsTemp = true },
				new() { Name = "Employee State Insurance (ESI) - Auto (2026-10 correction)", Amount = 72m, IsTemp = true }
			]
		};
		var model = SalarySlipProjection.ToPdfModel(dto, "Synthetic School");
		model.Earnings.Sum(x => x.Amount).ShouldBe(model.NetPay + model.TotalDeductions);
		model.NetPayInWords.ShouldBe("Ten Thousand Five Hundred Twenty Six Rupees And Fifty Five Paise only");
		var bytes = new PayslipPdfService().Generate(model);
		PayslipPdfTextAssertions.AssertReadable(bytes, "Rs. 272.73", "Rs. 181.82", model.NetPayInWords);
	}

	[Theory]
	[InlineData("Employee State Insurance (ESI) - Auto", "Employee ESI")]
	[InlineData("Employee State Insurance (ESI) - Auto (2026-10 correction)", "Employee ESI (2026-10 correction)")]
	[InlineData("Employee State Insurance (ESI) - Auto (2026-13 correction)", "Employee State Insurance (ESI) - Auto (2026-13 correction)")]
	[InlineData("Custom allowance - Auto", "Custom allowance - Auto")]
	public void Employee_component_labels_preserve_amounts_source_months_and_custom_names(string name, string expected)
	{
		var dto = new SalarySlipDto
		{
			MonthYear = "2026-11",
			ProcessedDate = DateTime.UtcNow,
			Earnings = [new() { Name = name, Amount = 3m, FullMonthAmount = 5m }],
			Deductions = [new() { Name = name, Amount = 73m }]
		};
		var model = SalarySlipProjection.ToPdfModel(dto, "Synthetic School");
		model.Earnings.Single().Name.ShouldBe(expected);
		model.Earnings.Single().Amount.ShouldBe(3m);
		model.Earnings.Single().FullMonthAmount.ShouldBe(5m);
		model.Deductions.Single().Name.ShouldBe(expected);
		model.Deductions.Single().Amount.ShouldBe(73m);
		dto.Earnings.Single().Name.ShouldBe(name);
		dto.Deductions.Single().Name.ShouldBe(name);
		PayslipPdfFormatting.FormatEmployeeComponentName(expected).ShouldBe(expected);
	}

	[Theory]
	[InlineData(DateTimeKind.Utc)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Issued_date_uses_the_Gujarat_payroll_timezone(DateTimeKind kind)
	{
		var dto = new SalarySlipDto { MonthYear = "2026-10", ProcessedDate = new DateTime(2026, 10, 2, 21, 0, 0, kind) };
		SalarySlipProjection.ToPdfModel(dto, "Synthetic School").GeneratedAt.ShouldBe(new DateTime(2026, 10, 3, 2, 30, 0));
	}

	[Fact]
	public void Stored_artifact_cannot_be_changed_through_input_or_returned_arrays()
	{
		var bytes = Encoding.ASCII.GetBytes("%PDF-synthetic immutable artifact");
		var document = new PayslipDocument(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"2026-10", 1, DateTime.UtcNow, Guid.NewGuid(), "{}", bytes);
		var hash = document.Sha256;
		bytes[0] = 0;
		var returned = document.Pdf;
		returned[0] = 0;
		document.Pdf[0].ShouldBe((byte) '%');
		document.Sha256.ShouldBe(hash);
	}

	[Theory]
	[InlineData("partial-attendance", 1, false)]
	[InlineData("esic-off", 1, false)]
	[InlineData("exempt-zero", 1, false)]
	[InlineData("long-multipage", 45, false)]
	[InlineData("draft", 1, true)]
	[InlineData("unicode", 1, false)]
	[InlineData("unicode-long", 45, false)]
	public void Employee_document_renders_authoritative_amounts_and_long_tables(string scenario, int count, bool draft)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var model = Model(scenario, count, draft);
		var bytes = new PayslipPdfService().Generate(model);
		Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
		bytes.Length.ShouldBeGreaterThan(1000);
		var output = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			File.WriteAllBytes(Path.Combine(output, $"{scenario}.pdf"), bytes);
		}
		if (!scenario.StartsWith("unicode", StringComparison.Ordinal)) AssertNoNullUnicodeMappings(bytes);
		PayslipPdfTextAssertions.AssertReadable(bytes,
			new[] { model.EmployeeName, model.Department!, model.Designation! }.Concat(model.Earnings.Select(x => x.Name)).ToArray());
	}

	[Theory]
	[InlineData("missing-text")]
	[InlineData("wrong-text")]
	[InlineData("wrong-mcid")]
	[InlineData("continued-span")]
	public void Unicode_regression_rejects_missing_incorrect_or_misassociated_recorded_text(string defect)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var bytes = new PayslipPdfService().Generate(Model("unicode", 1, false));
		PayslipPdfTextAssertions.AssertReadable(bytes, "દીપ્તિ શાહ");
		using var original = new MemoryStream(bytes);
		using var pdf = PdfReader.Open(original, PdfDocumentOpenMode.Modify);
		var span = FindSpan(pdf.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot"));
		span.ShouldNotBeNull();
		switch (defect)
		{
			case "missing-text": span.Elements.Remove("/Alt"); break;
			case "wrong-text": span.Elements.SetString("/Alt", "Another employee"); break;
			case "wrong-mcid": span.Elements.SetInteger("/K", 99999); break;
			case "continued-span": span.Elements["/K"] = new PdfArray(pdf, new PdfInteger(10), new PdfInteger(11)); break;
		}
		using var altered = new MemoryStream();
		pdf.Save(altered, false);
		Should.Throw<ShouldAssertException>(() => PayslipPdfTextAssertions.AssertReadable(altered.ToArray(), "દીપ્તિ શાહ"));
		if (defect is "missing-text" or "wrong-mcid")
			Should.Throw<ShouldAssertException>(() => PayslipPdfTextAssertions.AssertReadable(altered.ToArray()));
		PayslipPdfTextAssertions.AssertReadable(bytes, "દીપ્તિ શાહ"); // Never change the original artifact.

		static PdfDictionary? FindSpan(PdfItem? item)
		{
			if (item is PdfReference reference) item = reference.Value;
			if (item is PdfArray array) return array.Elements.Select(FindSpan).FirstOrDefault(x => x is not null);
			if (item is not PdfDictionary node) return null;
			return node.Elements.GetName("/S") == "/Span" && node.Elements.GetString("/Alt") == "દીપ્તિ શાહ"
				? node : FindSpan(node.Elements["/K"]);
		}
	}

	private static PayslipPdfModel Model(string scenario, int count, bool draft)
	{
		var unicode = scenario.StartsWith("unicode", StringComparison.Ordinal);
		var unit = Math.Round(5000m / count, 2, MidpointRounding.AwayFromZero);
		var earnings = Enumerable.Range(1, count).Select(i => new PayslipPdfLineItem(
			count == 1 ? "Basic salary" : unicode ? $"ગુજરાતી ભથ્થું {i}" : $"Earning {i}: a long descriptive component name for pagination",
			i == count ? 5000m - unit * (count - 1) : unit,
			2m * (i == count ? 5000m - unit * (count - 1) : unit))).ToList();
		var esi = scenario == "esic-off" ? Array.Empty<PayslipPdfLineItem>() : new[] {
			new PayslipPdfLineItem("Employee ESI", scenario == "exempt-zero" ? 0m : 38m) };
		var deduction = esi.Sum(x => x.Amount);
		return new PayslipPdfModel("Synthetic Layout Test School", "Synthetic address, Ahmedabad, Gujarat",
			count > 1 ? string.Join(" ", Enumerable.Repeat("Long employee name", 12))
				: unicode ? "દીપ્તિ શાહ" : "Synthetic Employee",
			"SYN-001", unicode ? "ગુજરાતી વિભાગ" : "Teaching",
			unicode ? "શિક્ષક" : "Teacher", "01 Oct 2026", "October 2026",
			"PS-0123456789abcdef0123456789abcdef-R1", "Finalized", draft, draft ? "Preview" : "Finalized",
			new DateTime(2026, 10, 3), 22m, 11m, 11m, 0m, 11m, .5m, null,
			"Synthetic Bank", "XXXX7890", "SBIN0001234", null, null,
			earnings, esi, 5000m, deduction, 5000m - deduction, IndianCurrencyWords.Format(5000m - deduction), null)
		{ FullGrossSalary = 10000m, GrossSalary = 5000m, InsuranceNumber = scenario == "esic-off" ? null : "1234567890" };
	}

	private static void AssertNoNullUnicodeMappings(byte[] pdf)
	{
		var binary = Encoding.Latin1.GetString(pdf);
		var maps = 0;
		foreach (Match stream in Regex.Matches(binary, @"stream\r?\n(?<data>[\s\S]*?)\r?\nendstream"))
		{
			var data = Encoding.Latin1.GetBytes(stream.Groups["data"].Value);
			// QuestPDF compresses font Unicode maps with zlib, independently of page content.
			if (data.Length < 2 || data[0] != 0x78) continue;
			using var compressed = new MemoryStream(data);
			using var decompressed = new ZLibStream(compressed, CompressionMode.Decompress);
			using var reader = new StreamReader(decompressed, Encoding.ASCII);
			var text = reader.ReadToEnd();
			if (!text.Contains("beginbfchar", StringComparison.Ordinal)) continue;
			maps++;
			Regex.IsMatch(text, @"<[0-9a-fA-F]+>\s+<0000>").ShouldBeFalse(
				"Payslip font mappings must not lose ligature text as NUL characters.");
		}
		maps.ShouldBeGreaterThan(0, "The regression must inspect actual font Unicode maps.");
	}
}
