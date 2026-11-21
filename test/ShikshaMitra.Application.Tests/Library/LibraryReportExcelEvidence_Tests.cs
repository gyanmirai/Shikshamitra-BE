using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using ShikshaMitra.Library.Reports;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReportExcelEvidence_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void H_ReportArtifact_Excel_Should_Preserve_Date_Context_And_Incomplete_Classification_Warnings(bool dated)
	{
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
		var bytes = GetRequiredService<LibraryExcelService>().GenerateReportExcel(report);
		using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
		var text = string.Join(" ", archive.Entries.Where(entry => entry.FullName.EndsWith(".xml", StringComparison.Ordinal))
			.Select(entry =>
			{
				using var reader = new StreamReader(entry.Open());
				return string.Join(" ", XDocument.Parse(reader.ReadToEnd()).DescendantNodes().OfType<XText>().Select(node => node.Value));
			}));
		text.ShouldContain("DATA-MARKER");

		text.ShouldContain("One saved due date needs review.");
		text.ShouldContain(dated ? "2026-11-18" : "The Library school clock is unavailable.");
	}
}
