using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PdfSharp.Pdf.IO;
using QuestPDF.Infrastructure;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReportPdf_Tests
{
	[Fact]
	[Trait("Module", "Library")]
	[Trait("Tier", "Important")]
	public async Task Export_Prints_Every_Row_Beyond_500_And_The_Actual_Record_Count()
	{
		QuestPDF.Settings.License = LicenseType.Community;
		const int rowCount = 505;
		var report = new LibraryReportPreviewDto
		{
			ReportType = LibraryReportTypes.BookMaster,
			TotalCount = rowCount,
			Page = 1,
			PageSize = rowCount,
			Columns = [new() { Id = "accession", Label = "Accession number" }],
			Rows = Enumerable.Range(1, rowCount).Select(number => new Dictionary<string, string?>
			{
				["accession"] = $"LIBROW-{number:D4}"
			}).ToList()
		};

		var bytes = await new LibraryPdfService().GenerateReportPdfAsync(report);
		var output = Environment.GetEnvironmentVariable("LIBRARY_PDF_VERIFY_DIRECTORY");
		if (!string.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			await File.WriteAllBytesAsync(Path.Combine(output, "library-report-505-rows.pdf"), bytes);
		}
		var printed = ReceiptPdfTextReader.Read(bytes);
		printed.ShouldContain("LIBROW-0001");
		printed.ShouldContain("LIBROW-0505");
		printed.ShouldContain("Records: 505");
		Regex.Matches(printed, @"LIBROW-\d{4}").Select(match => match.Value).Distinct().Count().ShouldBe(rowCount);
		using var stream = new MemoryStream(bytes);
		using var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
		document.PageCount.ShouldBeGreaterThan(1);
	}
}
