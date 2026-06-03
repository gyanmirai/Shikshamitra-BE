using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfFormatting;

internal static class PayslipPdfTableBlocks
{
	public static void ComposeLineTable(IContainer container, string title, IReadOnlyList<PayslipPdfLineItem> lines, decimal total, string headerBg, string headerColor)
	{
		container.Column(column =>
		{
			column.Item().Text(title).Bold().FontColor(headerColor);
			column.Item().PaddingTop(4).Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.RelativeColumn();
					columns.ConstantColumn(88);
				});

				table.Header(header =>
				{
					header.Cell().Element(c => HeaderCell(c, headerBg)).Text("Component").Bold().FontColor(headerColor);
					header.Cell().Element(c => HeaderCell(c, headerBg)).AlignRight().Text("Amount").Bold().FontColor(headerColor);
				});

				foreach (var line in lines)
				{
					table.Cell().Element(BodyCell).Text(line.Name);
					table.Cell().Element(BodyCell).AlignRight().Text(FormatCurrency(line.Amount));
				}

				if (lines.Count == 0)
				{
					table.Cell().ColumnSpan(2).Element(BodyCell).Text("No components").Italic().FontColor(Colors.Grey.Darken1);
				}

				table.Cell().Element(c => TotalCell(c, headerBg)).Text("Total").Bold().FontColor(headerColor);
				table.Cell().Element(c => TotalCell(c, headerBg)).AlignRight().Text(FormatCurrency(total)).Bold().FontColor(headerColor);
			});
		});
	}

	public static void ComposeKeyValueBlock(IContainer container, string title, IEnumerable<(string Label, string? Value)> rows)
	{
		container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(column =>
		{
			column.Item().Text(title).Bold().FontColor("#0F172A");
			column.Item().PaddingTop(6).Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.ConstantColumn(88);
					columns.RelativeColumn();
				});

				foreach (var row in rows)
				{
					table.Cell().PaddingBottom(4).Text(row.Label).FontColor(Colors.Grey.Darken1);
					table.Cell().PaddingBottom(4).Text(string.IsNullOrWhiteSpace(row.Value) ? "-" : row.Value).Bold();
				}
			});
		});
	}

	public static void AddMetric(TableDescriptor table, string label, string? value)
	{
		table.Cell().PaddingBottom(6).Column(column =>
		{
			column.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
			column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).Bold().FontColor("#0F172A");
		});
	}
}
