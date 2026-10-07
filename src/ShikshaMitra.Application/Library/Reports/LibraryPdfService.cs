using System;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using ShikshaMitra.Common.PDF;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Library.Reports;

public class LibraryPdfService : ITransientDependency
{
	public Task<byte[]> GenerateReportPdfAsync(LibraryReportPreviewDto report)
	{
		var bytes = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4.Landscape());
				page.Margin(20);
				page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Grey.Darken4).WithPortableFonts());
				page.Header().Element(c => ComposeHeader(c, report));
				page.Content().Element(c => ComposeTable(c, report));
				page.Footer().AlignRight().Text(text =>
				{
					text.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
					text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
					text.Span(" of ").FontSize(7).FontColor(Colors.Grey.Medium);
					text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
				});
			});
		}).GeneratePdf();

		return Task.FromResult(bytes);
	}

	private static void ComposeHeader(QuestPDF.Infrastructure.IContainer container, LibraryReportPreviewDto report)
	{
		container.Background("#1E3A5F").Padding(14).Row(row =>
		{
			row.RelativeItem().Column(left =>
			{
				left.Item().Text(report.ReportType).FontSize(16).Bold().FontColor(Colors.White);
				left.Item().Text("Library Report").FontSize(8).FontColor("#BFDBFE");
			});
			row.ConstantItem(180).AlignRight().AlignMiddle().Column(right =>
			{
				right.Item().AlignRight().Text($"Generated: {DateTime.UtcNow:dd MMM yyyy, HH:mm} UTC")
					.FontSize(7.5f).FontColor("#BFDBFE");
				right.Item().AlignRight().Text($"Records: {report.TotalCount}")
					.FontSize(7.5f).FontColor("#BFDBFE");
			});
		});
	}

	private static void ComposeTable(QuestPDF.Infrastructure.IContainer container, LibraryReportPreviewDto report)
	{
		if (report.Rows.Count == 0)
		{
			container.Background("#EEF2F7").Padding(20).AlignCenter()
				.Text("No records found for the selected filters.")
				.FontSize(10).FontColor(Colors.Grey.Medium);
			return;
		}

		container.Table(table =>
		{
			table.ColumnsDefinition(cols =>
			{
				foreach (var _ in report.Columns)
				{
					cols.RelativeColumn();
				}
			});

			table.Header(header =>
			{
				foreach (var column in report.Columns)
				{
					header.Cell()
						.Background("#1E3A5F")
						.PaddingHorizontal(6)
						.PaddingVertical(6)
						.Text(column.Label)
						.FontSize(8)
						.Bold()
						.FontColor(Colors.White);
				}
			});

			var rowIndex = 0;
			foreach (var reportRow in report.Rows.Take(500))
			{
				var background = rowIndex % 2 == 1 ? "#F8FAFC" : "#FFFFFF";
				foreach (var column in report.Columns)
				{
					reportRow.TryGetValue(column.Id, out var value);
					table.Cell()
						.Background(background)
						.BorderBottom(0.5f)
						.BorderColor("#CBD5E1")
						.PaddingHorizontal(6)
						.PaddingVertical(5)
						.Text(value ?? string.Empty)
						.FontSize(7.5f);
				}

				rowIndex++;
			}
		});
	}
}