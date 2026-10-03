using System;
using System.IO;
using System.Linq;
using System.Text;
using QuestPDF.Infrastructure;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class PayslipPdfLayout_Tests
{
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
		document.Pdf[0].ShouldBe((byte)'%');
		document.Sha256.ShouldBe(hash);
	}

	[Theory]
	[InlineData("partial-attendance", 1, false)]
	[InlineData("esic-off", 1, false)]
	[InlineData("exempt-zero", 1, false)]
	[InlineData("long-multipage", 45, false)]
	[InlineData("draft", 1, true)]
	public void Employee_document_renders_authoritative_amounts_and_long_tables(string scenario, int count, bool draft)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var unit = Math.Round(5000m / count, 2, MidpointRounding.AwayFromZero);
		var earnings = Enumerable.Range(1, count).Select(i => new PayslipPdfLineItem(
			count == 1 ? "Basic salary" : $"Earning {i}: a long descriptive component name for pagination",
			i == count ? 5000m - unit * (count - 1) : unit,
			2m * (i == count ? 5000m - unit * (count - 1) : unit))).ToList();
		var esi = scenario == "esic-off" ? Array.Empty<PayslipPdfLineItem>() : new[] {
			new PayslipPdfLineItem("Employee ESI", scenario == "exempt-zero" ? 0m : 38m) };
		var deduction = esi.Sum(x => x.Amount);
		var model = new PayslipPdfModel("Synthetic Layout Test School", "Synthetic address, Ahmedabad, Gujarat",
			count > 1 ? string.Join(" ", Enumerable.Repeat("Long employee name", 12)) : "Synthetic Employee",
			"SYN-001", "Teaching", "Teacher", "01 Oct 2026", "October 2026",
			"PS-0123456789abcdef0123456789abcdef-R1", "Finalized", draft, draft ? "Preview" : "Finalized",
			new DateTime(2026, 10, 3), 22m, 11m, 11m, 0m, 11m, .5m, null,
			"Synthetic Bank", "XXXX7890", "SBIN0001234", null, null,
			earnings, esi, 5000m, deduction, 5000m - deduction, IndianCurrencyWords.Format(5000m - deduction), null)
			{ FullGrossSalary = 10000m, GrossSalary = 5000m, InsuranceNumber = scenario == "esic-off" ? null : "1234567890" };
		var bytes = new PayslipPdfService().Generate(model);
		Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
		bytes.Length.ShouldBeGreaterThan(1000);
		var output = Environment.GetEnvironmentVariable("PAYSLIP_VERIFY_ARTIFACTS");
		if (!string.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			File.WriteAllBytes(Path.Combine(output, $"{scenario}.pdf"), bytes);
		}
	}
}
