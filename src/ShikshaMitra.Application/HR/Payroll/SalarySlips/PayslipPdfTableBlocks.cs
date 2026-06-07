using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfFormatting;

internal static class PayslipPdfTableBlocks
{
	private const string Ink = "#111827";
	private const string Muted = "#6B7280";
	private const string Border = "#E5E7EB";

	public static void ComposeLineTable(IContainer container, string title, IReadOnlyList<PayslipPdfLineItem> lines, decimal total)
	{
		container.Border(1).BorderColor(Border).Column(column =>
		{
			column.Item().Background("#F9FAFB").BorderBottom(1).BorderColor(Border).PaddingVertical(6).PaddingHorizontal(8)
				.Text(title).FontSize(8).Bold().FontColor(Ink);

			column.Item().Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.RelativeColumn();
					columns.ConstantColumn(92);
				});

				table.Header(header =>
				{
					header.Cell().Element(HeaderCell).Text("Component").Bold().FontColor(Muted);
					header.Cell().Element(HeaderCell).AlignRight().Text("Amount").Bold().FontColor(Muted);
				});

				if (lines.Count == 0)
				{
					table.Cell().ColumnSpan(2).Element(BodyCell).Text("No components").Italic().FontColor(Muted);
				}
				else
				{
					foreach (var line in lines)
					{
						table.Cell().Element(BodyCell).Text(line.Name).FontColor(Muted);
						table.Cell().Element(BodyCell).AlignRight().Text(FormatCurrency(line.Amount)).Bold().FontColor(Ink);
					}
				}

				table.Cell().Element(TotalCell).Text("Total").Bold().FontColor(Ink);
				table.Cell().Element(TotalCell).AlignRight().Text(FormatCurrency(total)).Bold().FontColor(Ink);
			});
		});
	}

	public static void ComposeKeyValueBlock(IContainer container, string title, IEnumerable<(string Label, string? Value)> rows)
	{
		container.Border(1).BorderColor(Border).Padding(10).Column(column =>
		{
			column.Item().Text(title).Bold().FontColor(Ink);
			column.Item().PaddingTop(6).Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.ConstantColumn(88);
					columns.RelativeColumn();
				});

				foreach (var row in rows)
				{
					table.Cell().PaddingBottom(4).Text(row.Label).FontColor(Muted);
					table.Cell().PaddingBottom(4).Text(string.IsNullOrWhiteSpace(row.Value) ? "-" : row.Value).Bold();
				}
			});
		});
	}

	public static void AddMetric(TableDescriptor table, string label, string? value)
	{
		table.Cell().PaddingBottom(6).Column(column =>
		{
			column.Item().Text(label).FontSize(7).Bold().FontColor(Muted);
			column.Item().PaddingTop(1).Text(string.IsNullOrWhiteSpace(value) ? "-" : value).Bold().FontColor(Ink);
		});
	}
}
