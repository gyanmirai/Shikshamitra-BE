using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Infrastructure;
using ShikshaMitra.Finance.Accounts.Reconciliation;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReportPdfEvidence_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Module", "Library")]
	[Trait("Tier", "Important")]
	public async Task H_ReportArtifact_Pdf_Should_Print_Date_Context_And_Incomplete_Classification_Warnings(bool dated)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var report = new LibraryReportPreviewDto
		{
			ReportType = LibraryReportTypes.OverdueBooks, TotalCount = 1, PageSize = 1,
			Columns = [new() { Id = "title", Label = "Title" }],
			Rows = [new Dictionary<string, string?> { ["title"] = "DATA-MARKER" }],
			SchoolAsOfDate = dated ? new DateOnly(2026, 11, 18) : null,
			SchoolDateSource = dated ? "ExplicitSchoolDate" : "Unavailable",
			SchoolDateUnavailableReason = dated ? null : "The Library school clock is unavailable.",
			OverdueClassificationAvailable = false, UnknownDueDateCount = 1,
			QueryWarnings = ["One saved due date needs review."]
		};
		var bytes = await new LibraryPdfService().GenerateReportPdfAsync(report);
		var output = Environment.GetEnvironmentVariable("LIBRARY_PDF_VERIFY_DIRECTORY");
		if (!string.IsNullOrWhiteSpace(output))
		{
			Directory.CreateDirectory(output);
			await File.WriteAllBytesAsync(Path.Combine(output, dated ? "library-report-school-date.pdf" : "library-report-clock-unavailable.pdf"), bytes);
		}
		var text = ReceiptPdfTextReader.Read(bytes);
		text.ShouldContain("DATA-MARKER");

		text.ShouldContain("One saved due date needs review.");
		text.ShouldContain(dated ? "2026-11-18" : "The Library school clock is unavailable.");
	}
}
