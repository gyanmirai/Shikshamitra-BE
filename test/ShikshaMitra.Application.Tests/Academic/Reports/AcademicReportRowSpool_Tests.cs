using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Infrastructure;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Reports;

public class AcademicReportRowSpool_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Pdf_Source_Can_Replay_All_Rows_Without_Filling_The_Metadata_Row_List(bool timetable)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var original = new AcademicReportPreviewDto
		{
			ReportType = timetable ? AcademicReportTypes.ClasswiseTimetable : AcademicReportTypes.ChapterProgressReport,
			Columns = [new() { Id = "Name", Label = "Name" }, new() { Id = "Status", Label = "Status" }],
			Rows = Enumerable.Range(0, 260).Select(index => new Dictionary<string, string?>
			{
				["Name"] = $"Row {index}: \"quoted\"\nनमस्ते", ["Status"] = index % 2 == 0 ? "Published" : null
			}).ToList()
		};
		await using var spool = await AcademicReportRowSpool.CreateAsync(AcademicReportExportRows.FromReport(original).Rows, CancellationToken.None);
		spool.Count.ShouldBe(260);
		foreach (var replay in new[] { spool.ReadRows().ToList(), spool.ReadRows().ToList() })
			for (var index = 0; index < original.Rows.Count; index++)
				replay[index].ShouldBe(original.Rows[index]);
		var metadata = new AcademicReportPreviewDto { ReportType = original.ReportType, Columns = original.Columns, TotalCount = spool.Count };
		using var file = new MemoryStream();
		if (timetable) new TimetableReportsPdfService().WriteReportPdf(metadata, spool.ReadRows(), spool.Count, file);
		else new AcademicPdfService().WriteReportPdf(metadata, spool.ReadRows(), spool.Count, file);
		metadata.Rows.ShouldBeEmpty();
		Encoding.ASCII.GetString(file.ToArray(), 0, 5).ShouldBe("%PDF-");
		file.Length.ShouldBeGreaterThan(5_000);
	}

	[Fact]
	public async Task Pdf_Row_Replay_Observes_Cancellation()
	{
		using var cancellation = new CancellationTokenSource();
		var report = new AcademicReportPreviewDto { Rows = [new() { ["value"] = "value" }] };
		await using var spool = await AcademicReportRowSpool.CreateAsync(AcademicReportExportRows.FromReport(report).Rows, cancellation.Token);
		cancellation.Cancel();
		Should.Throw<OperationCanceledException>(() => spool.ReadRows().ToList());
	}
}
