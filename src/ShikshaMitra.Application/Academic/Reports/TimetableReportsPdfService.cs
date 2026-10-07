using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

/// <summary>
/// Generates styled PDF reports for timetable report types.
/// Uses QuestPDF Fluent API directly for a rich, professional layout.
/// </summary>
public class TimetableReportsPdfService : ITransientDependency
{
	private const string AccentColor = "#1E3A5F";   // Dark navy header
	private const string AccentLight = "#EEF2F7";   // Header cell bg
	private const string AltRowBg = "#F8FAFC";       // Alternating row bg
	private const string BorderColor = "#CBD5E1";
	private const string TagFree = "#22C55E";        // Green for "Free"
	private const string TagScheduled = "#3B82F6";   // Blue for "Scheduled"
	private const string TagDraft = "#F59E0B";       // Amber for "Draft"
	private const string TagPublished = "#10B981";   // Emerald for "Published"

	public static bool IsTimetableReport(string reportType) =>
		reportType is AcademicReportTypes.ClasswiseTimetable
			or AcademicReportTypes.TeacherwiseTimetable
			or AcademicReportTypes.RoomUtilizationReport
			or AcademicReportTypes.SubjectwiseTimetableDistribution
			or AcademicReportTypes.FreePeriodsReport
			or AcademicReportTypes.TimetableChangeLog;

	public async Task<byte[]> GenerateAsync(AcademicReportPreviewDto report)
	{
		return await Task.FromResult(CreateDocument(report).GeneratePdf());
	}

	public void WriteReportPdf(AcademicReportPreviewDto report, Stream destination)
	{
		CreateDocument(report).GeneratePdf(destination);
	}

	internal void WriteReportPdf(AcademicReportPreviewDto report, IEnumerable<Dictionary<string, string?>> rows,
		int rowCount, Stream destination) => CreateDocument(report, rows, rowCount).GeneratePdf(destination);

	private static IDocument CreateDocument(AcademicReportPreviewDto report,
		IEnumerable<Dictionary<string, string?>>? rows = null, int? rowCount = null)
	{
		return Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4.Landscape());
				page.Margin(20);
				page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Grey.Darken4).WithPortableFonts());

				page.Content().Column(col =>
				{
					col.Spacing(10);
					col.Item().Element(c => ComposeHeader(c, report));
					col.Item().Element(c => ComposeSummaryBar(c, report));
					col.Item().Element(c => ComposeTable(c, report, rows ?? report.Rows, rowCount ?? report.Rows.Count));
				});

				page.Footer().Element(ComposeFooter);
			});
		});
	}

	private static void ComposeHeader(IContainer container, AcademicReportPreviewDto report)
	{
		container.Column(col =>
		{
			col.Item().Background(AccentColor).Padding(14).Row(row =>
			{
				row.RelativeItem().Column(left =>
				{
					left.Item().Text(report.ReportType)
						.FontSize(16).Bold().FontColor(Colors.White);
					left.Item().Text("Timetable Report • School Management System")
						.FontSize(8).FontColor("#93C5FD");
				});
				row.ConstantItem(160).AlignRight().AlignMiddle().Column(right =>
				{
					right.Item().AlignRight().Text($"Generated: {DateTime.Now:dd MMM yyyy, hh:mm tt}")
						.FontSize(7.5f).FontColor("#BFDBFE");
					right.Item().AlignRight().Text($"Records: {report.TotalCount}")
						.FontSize(7.5f).FontColor("#BFDBFE");
				});
			});
		});
	}

	private static void ComposeSummaryBar(IContainer container, AcademicReportPreviewDto report)
	{
		// Only shown when filters are active
		var filters = report.NormalizedFilters;
		var hasFilters = (filters.ClassIds?.Any() == true) || (filters.TeacherIds?.Any() == true)
			|| (filters.RoomIds?.Any() == true) || (filters.SubjectIds?.Any() == true);
		if (!hasFilters) return;

		container.Background(AccentLight).BorderLeft(3).BorderColor(AccentColor).PaddingHorizontal(10).PaddingVertical(6)
			.Text("Filters applied — results are scoped to the selected classes, teachers, rooms or subjects.")
			.FontSize(8).FontColor(AccentColor).Italic();
	}

	private static void ComposeTable(IContainer container, AcademicReportPreviewDto report,
		IEnumerable<Dictionary<string, string?>> rows, int rowCount)
	{
		if (rowCount == 0)
		{
			container.Background(AccentLight).Padding(20).AlignCenter()
				.Text("No records found for the selected filters.")
				.FontSize(10).FontColor(Colors.Grey.Medium);
			return;
		}

		container.Table(table =>
		{
			table.ColumnsDefinition(cols =>
			{
				foreach (var _ in report.Columns)
					cols.RelativeColumn();
			});

			// Header
			table.Header(header =>
			{
				foreach (var col in report.Columns)
				{
					header.Cell()
						.Background(AccentColor)
						.BorderBottom(1.5f).BorderColor(AccentColor)
						.PaddingHorizontal(8).PaddingVertical(7)
						.Text(col.Label)
						.FontSize(8).Bold().FontColor(Colors.White);
				}
			});

			// Rows
			var rowIndex = 0;
			foreach (var row in rows)
			{
				var isAlt = rowIndex % 2 == 1;
				var bg = isAlt ? AltRowBg : "#FFFFFF";

				foreach (var col in report.Columns)
				{
					var value = row.TryGetValue(col.Id, out var v) ? v ?? string.Empty : string.Empty;
					var cell = table.Cell()
						.Background(bg)
						.BorderBottom(0.5f).BorderColor(BorderColor)
						.PaddingHorizontal(8).PaddingVertical(5);

					// Special badge rendering for Status column
					if (col.Id == "Status")
					{
						cell.Element(c => RenderStatusBadge(c, value));
					}
					else
					{
						cell.Text(value).FontSize(8).FontColor(Colors.Grey.Darken4);
					}
				}
				rowIndex++;
			}
		});
	}

	private static void RenderStatusBadge(IContainer container, string value)
	{
		string bg;
		string fg;
		switch (value)
		{
			case "Free":
				bg = TagFree; fg = Colors.White; break;
			case "Scheduled":
				bg = TagScheduled; fg = Colors.White; break;
			case "Draft":
				bg = TagDraft; fg = Colors.White; break;
			case "Published":
				bg = TagPublished; fg = Colors.White; break;
			default:
				bg = Colors.Grey.Lighten2; fg = Colors.Grey.Darken3; break;
		}

		container.AlignLeft().Background(bg).Padding(3)
			.Text(value).FontSize(7.5f).Bold().FontColor(fg);
	}

	private static void ComposeFooter(IContainer container)
	{
		container.Row(row =>
		{
			row.RelativeItem().AlignLeft().Text("ShikshaMitra — Academic Timetable Reports")
				.FontSize(7).FontColor(Colors.Grey.Medium);
			row.RelativeItem().AlignRight().Text(text =>
			{
				text.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
				text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
				text.Span(" of ").FontSize(7).FontColor(Colors.Grey.Medium);
				text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
			});
		});
	}
}