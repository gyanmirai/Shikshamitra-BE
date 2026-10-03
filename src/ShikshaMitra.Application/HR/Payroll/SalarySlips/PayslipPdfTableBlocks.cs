using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfFormatting;

internal static class PayslipPdfTableBlocks
{
	private const string Ink = "#111827";
	private const string Muted = "#6B7280";
	private const string Border = "#E5E7EB";

	public static void ComposeLineTable(IContainer container, string title, IReadOnlyList<PayslipPdfLineItem> lines, decimal total)
	{
		var showMonthlyRate = lines.Any(x => x.FullMonthAmount.HasValue);
		container.Border(1).BorderColor(Border).Column(column =>
		{
			column.Item().Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.RelativeColumn();
					if (showMonthlyRate) columns.ConstantColumn(76);
					columns.ConstantColumn(92);
				});

				table.Header(header =>
				{
					header.Cell().ColumnSpan(showMonthlyRate ? 3u : 2u).Element(HeaderCell)
						.Text(title).FontSize(8).Bold().FontColor(Ink);
					header.Cell().Element(HeaderCell).Text("Component").Bold().FontColor(Muted);
					if (showMonthlyRate) header.Cell().Element(HeaderCell).AlignRight().Text("Monthly rate").Bold().FontColor(Muted);
					header.Cell().Element(HeaderCell).AlignRight().Text(showMonthlyRate ? "Earned" : "Amount").Bold().FontColor(Muted);
				});

				if (lines.Count == 0)
				{
					table.Cell().ColumnSpan(showMonthlyRate ? 3u : 2u).Element(BodyCell).Text("No components").Italic().FontColor(Muted);
				}
				else
				{
					foreach (var line in lines)
					{
						table.Cell().Element(BodyCell).Text(line.Name).FontColor(Muted);
						if (showMonthlyRate) table.Cell().Element(BodyCell).AlignRight()
							.Text(line.FullMonthAmount.HasValue ? FormatCurrency(line.FullMonthAmount.Value) : "-").FontColor(Muted);
						table.Cell().Element(BodyCell).AlignRight().Text(FormatCurrency(line.Amount)).Bold().FontColor(Ink);
					}
				}

				table.Cell().Element(TotalCell).Text("Total").Bold().FontColor(Ink);
				if (showMonthlyRate) table.Cell().Element(TotalCell).AlignRight()
					.Text(FormatCurrency(lines.Sum(x => x.FullMonthAmount ?? 0m))).Bold().FontColor(Ink);
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
					if (string.IsNullOrWhiteSpace(row.Value)) continue;
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
