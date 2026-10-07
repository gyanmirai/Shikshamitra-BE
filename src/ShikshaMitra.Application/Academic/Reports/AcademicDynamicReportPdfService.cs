using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

public class AcademicDynamicReportPdfService : ITransientDependency
{
	private const string Accent = "#123B5D";
	private const string AccentSoft = "#EAF2F8";
	private const string Border = "#CBD5E1";

	public byte[] Generate(AcademicDynamicReportSpecDto plan, AcademicReportPreviewDto report) =>
		CreateDocument(plan, report).GeneratePdf();

	public void WriteReportPdf(AcademicDynamicReportSpecDto plan, AcademicReportPreviewDto report, Stream destination) =>
		CreateDocument(plan, report).GeneratePdf(destination);

	private static IDocument CreateDocument(AcademicDynamicReportSpecDto plan, AcademicReportPreviewDto report) =>
		Document.Create(document =>
		{
			document.Page(page =>
			{
				page.Size(PageSizes.A4.Landscape());
				page.Margin(20);
				page.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken4).WithPortableFonts());
				page.Header().Element(container => ComposeHeader(container, plan, report));
				page.Content().PaddingTop(10).Element(container => ComposeContent(container, report));
				page.Footer().Element(ComposeFooter);
			});
		});

	private static void ComposeHeader(IContainer container, AcademicDynamicReportSpecDto plan, AcademicReportPreviewDto report)
	{
		container.Column(column =>
		{
			column.Item().Background(Accent).Padding(14).Row(row =>
			{
				row.RelativeItem().Column(left =>
				{
					left.Item().Text(plan.Title).FontSize(16).Bold().FontColor(Colors.White);
					left.Item().Text($"Dynamic {plan.Module} report · {plan.Dataset}").FontSize(8).FontColor("#BFDBFE");
				});
				row.ConstantItem(160).AlignRight().AlignMiddle().Column(right =>
				{
					right.Item().AlignRight().Text($"Generated: {DateTime.UtcNow.AddMinutes(330):dd MMM yyyy, hh:mm tt} IST")
						.FontSize(7).FontColor("#BFDBFE");
					right.Item().AlignRight().Text($"Records: {report.TotalCount}").FontSize(7).FontColor("#BFDBFE");
				});
			});
			column.Item().Background(AccentSoft).PaddingHorizontal(10).PaddingVertical(7).Column(summary =>
			{
				summary.Item().Text(Clean(plan.Summary)).FontSize(8.5f).SemiBold();
				summary.Item().Text(Clean(report.FilterSummary)).FontSize(7).FontColor(Colors.Grey.Darken1);
			});
		});
	}

	private static void ComposeContent(IContainer container, AcademicReportPreviewDto report)
	{
		if (report.Rows.Count == 0)
		{
			container.Background(AccentSoft).Padding(24).AlignCenter()
				.Text("No authorized curriculum records matched this report request.")
				.FontSize(10).FontColor(Colors.Grey.Medium);
			return;
		}

		container.Table(table =>
		{
			table.ColumnsDefinition(columns =>
			{
				foreach (var _ in report.Columns) columns.RelativeColumn();
			});
			table.Header(header =>
			{
				foreach (var column in report.Columns)
				{
					header.Cell().Background(Accent).BorderBottom(1).BorderColor(Accent)
						.PaddingHorizontal(6).PaddingVertical(6).Text(column.Label).Bold().FontColor(Colors.White);
				}
			});

			for (var rowIndex = 0; rowIndex < report.Rows.Count; rowIndex++)
			{
				var row = report.Rows[rowIndex];
				var background = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
				foreach (var column in report.Columns)
				{
					row.TryGetValue(column.Id, out var value);
					table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Border)
						.PaddingHorizontal(6).PaddingVertical(4).Text(Clean(value));
				}
			}
		});
	}

	private static void ComposeFooter(IContainer container)
	{
		container.Row(row =>
		{
			row.RelativeItem().Text("Generated securely by ShikshaMitra AI").FontSize(7).FontColor(Colors.Grey.Medium);
			row.RelativeItem().AlignRight().Text(text =>
			{
				text.Span("Page ");
				text.CurrentPageNumber();
				text.Span(" of ");
				text.TotalPages();
			});
		});
	}

	private static string Clean(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return "—";
		var text = Regex.Replace(value, "<[^>]*>", string.Empty);
		return WebUtility.HtmlDecode(text).Trim();
	}
}