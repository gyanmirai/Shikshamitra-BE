using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HRPdfService : ITransientDependency
{
	public const int MaxPdfRows = 500;

	public static int GetIncludedRowCount(HrReportPreviewDto report) =>
		Math.Min(report.Rows.Count, MaxPdfRows);

	public Task<byte[]> GenerateReportPdf(HrReportPreviewDto report)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		var columnCount = Math.Max(report.Columns.Count, 1);
		var fontSize = columnCount switch
		{
			<= 6 => 8.5f,
			<= 12 => 7f,
			<= 20 => 6f,
			_ => 5f
		};
		var pageSize = columnCount > 12
			? PageSizes.A3.Landscape()
			: PageSizes.A4.Landscape();

		var bytes = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(pageSize);
				page.Margin(columnCount > 12 ? 12 : 20);
				page.PageColor(Colors.White);
				page.DefaultTextStyle(style => style.FontSize(fontSize).FontColor(Colors.Grey.Darken4).WithPortableFonts());
				page.Header().PaddingBottom(8).Column(column =>
				{
					column.Item()
						.Text(report.ReportType)
						.FontSize(16)
						.SemiBold()
						.FontColor("#1E3A5F");

					var includedRowCount = GetIncludedRowCount(report);
					if (report.TotalCount > includedRowCount || report.Rows.Count > includedRowCount)
					{
						column.Item()
							.PaddingTop(2)
							.Text($"Showing the first {includedRowCount:N0} of {report.TotalCount:N0} rows. Use Excel to export the complete result.")
							.FontSize(8)
							.FontColor(Colors.Orange.Darken3);
					}
				});
				page.Content().Element(content => ComposeTable(content, report, fontSize));
				page.Footer().AlignRight().Text(text =>
				{
					text.Span("Page ").FontSize(7);
					text.CurrentPageNumber().FontSize(7);
					text.Span(" of ").FontSize(7);
					text.TotalPages().FontSize(7);
				});
			});
		}).GeneratePdf();

		return Task.FromResult(bytes);
	}

	private static void ComposeTable(
		QuestPDF.Infrastructure.IContainer container,
		HrReportPreviewDto report,
		float fontSize)
	{
		if (report.Columns.Count == 0)
		{
			container
				.Background("#F8FAFC")
				.Padding(20)
				.AlignCenter()
				.Text("No columns were selected for this report.");
			return;
		}

		if (report.Rows.Count == 0)
		{
			container
				.Background("#F8FAFC")
				.Padding(20)
				.AlignCenter()
				.Text("No records found for the selected filters.");
			return;
		}

		container.Table(table =>
		{
			table.ColumnsDefinition(columns =>
			{
				foreach (var _ in report.Columns)
				{
					columns.RelativeColumn();
				}
			});

			table.Header(header =>
			{
				foreach (var column in report.Columns)
				{
					header.Cell()
						.Background("#1E3A5F")
						.PaddingHorizontal(2)
						.PaddingVertical(3)
						.Text(Sanitize(column.Label))
						.FontSize(fontSize)
						.SemiBold()
						.FontColor(Colors.White);
				}
			});

			var rowIndex = 0;
			foreach (var reportRow in report.Rows.Take(MaxPdfRows))
			{
				var background = rowIndex % 2 == 0 ? "#FFFFFF" : "#F8FAFC";
				foreach (var column in report.Columns)
				{
					reportRow.TryGetValue(column.Id, out var value);
					table.Cell()
						.Background(background)
						.BorderBottom(0.5f)
						.BorderColor("#CBD5E1")
						.PaddingHorizontal(2)
						.PaddingVertical(2)
						.Text(Sanitize(value ?? string.Empty))
						.FontSize(fontSize);
				}

				rowIndex++;
			}
		});
	}

	private static string Sanitize(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}

		var withoutMarkup = value.Contains('<') && value.Contains('>')
			? Regex.Replace(value, "<[^>]*>", string.Empty)
			: value;
		return withoutMarkup.Contains('&')
			? WebUtility.HtmlDecode(withoutMarkup)
			: withoutMarkup;
	}
}