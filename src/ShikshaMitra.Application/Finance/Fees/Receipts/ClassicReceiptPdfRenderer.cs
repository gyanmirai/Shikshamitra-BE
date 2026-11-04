using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.HR.Payroll.SalarySlips;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

/// <summary>The shared classic design for collection, charge and refund documents.</summary>
public static class ClassicReceiptPdfRenderer
{
	private const string Ink = "#172B3A";
	private const string Muted = "#63717D";
	private const string Border = "#D8DFE4";
	private const string HeaderFill = "#EAF0F2";
	private static string Money(decimal? amount) => amount.HasValue
		? $"₹ {amount.Value.ToString("N2", CultureInfo.GetCultureInfo("en-IN"))}" : "—";

	public static string Period(DateTime? start, DateTime? end, string? fallback = null)
	{
		if (!start.HasValue) return string.IsNullOrWhiteSpace(fallback) ? "—" : fallback;
		var first = start.Value.ToString("MMM yyyy", CultureInfo.InvariantCulture);
		return end.HasValue && (start.Value.Year != end.Value.Year || start.Value.Month != end.Value.Month)
			? $"{first} - {end.Value.ToString("MMM yyyy", CultureInfo.InvariantCulture)}" : first;
	}

	public static byte[] Render(ReceiptDocumentModel model)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		PortablePdfFonts.EnsureRegistered();
		return Document.Create(document => document.Page(page =>
		{
			page.Size(PageSizes.A4);
			page.Margin(36);
			page.DefaultTextStyle(style => style.FontSize(9).FontColor(Ink).WithPortableFonts());
			page.Header().Column(header =>
			{
				header.Item().PaddingBottom(18).Row(row =>
				{
					if (model.School.Logo != null)
						row.ConstantItem(58).Height(54).Image(model.School.Logo).FitArea();
					row.RelativeItem().PaddingHorizontal(12).AlignMiddle().Column(school =>
					{
						school.Item().AlignCenter().Text(model.School.Name).FontSize(19).Bold();
						if (!string.IsNullOrWhiteSpace(model.School.Address))
							school.Item().PaddingTop(8).AlignCenter().Text(model.School.Address).FontSize(9).FontColor(Muted);
					});
					if (model.School.Logo != null) row.ConstantItem(58);
				});
				header.Item().BorderTop(.8f).BorderBottom(.8f).BorderColor(Ink).PaddingVertical(15).Column(title =>
				{
					title.Item().AlignCenter().Text(model.Title).FontSize(18).Bold();
					if (!string.IsNullOrWhiteSpace(model.AcademicYear))
						title.Item().PaddingTop(7).AlignCenter().Text($"ACADEMIC SESSION {model.AcademicYear}").FontSize(8).FontColor(Muted);
				});
				header.Item().PaddingTop(16).Element(container => Fields(container,
					[new("Receipt no.", model.Number), new("Receipt date", model.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)),
					 new("Status", model.Status)], omitEmpty: true));
			});
			page.Content().PaddingTop(16).Column(content =>
			{
				content.Spacing(17);
				content.Item().Border(.6f).BorderColor(Border).Padding(12).Column(student =>
				{
					student.Item().Text("STUDENT DETAILS").FontSize(8).Bold().FontColor(Muted);
					student.Item().PaddingTop(12).Element(container => Fields(container, model.StudentFields));
				});
				content.Item().Column(payment =>
				{
					payment.Item().Text("PAYMENT DETAILS").FontSize(8).Bold();
					payment.Item().PaddingTop(11).Element(container => Fields(container, model.PaymentFields));
				});
				content.Item().Column(fees =>
				{
					fees.Item().PaddingBottom(10).Text(model.IsRefund ? "REFUND BREAKDOWN" : "FEE BREAKDOWN").FontSize(8).Bold();
					fees.Item().Element(container => LineTable(container, model));
				});
				content.Item().ShowEntire().Column(summary =>
				{
					summary.Item().BorderTop(.6f).BorderColor(Border).PaddingTop(13).Row(row =>
					{
						row.RelativeItem().AlignMiddle().Text(model.IsRefund ? "REFUND AMOUNT" : "AMOUNT RECEIVED").FontSize(8).Bold().FontColor(Muted);
						row.AutoItem().Text(Money(model.Amount)).FontSize(22).Bold();
					});
					summary.Item().PaddingTop(12).Text($"Amount in words: {IndianCurrencyWords.Format(model.Amount)}").SemiBold();
					if (!string.IsNullOrWhiteSpace(model.RelatedReceipt))
						summary.Item().PaddingTop(9).Text($"Source receipt: {model.RelatedReceipt}").FontSize(8).FontColor(Muted);
					summary.Item().PaddingTop(40).AlignRight().Width(145).Column(signature =>
					{
						signature.Item().LineHorizontal(.6f).LineColor(Border);
						signature.Item().PaddingTop(7).AlignCenter().Text("Authorized signatory").FontSize(8).FontColor(Muted);
					});
				});
			});
			page.Footer().PaddingTop(12).BorderTop(.5f).BorderColor(Border).PaddingTop(6).AlignRight().Text(text =>
			{
				text.DefaultTextStyle(style => style.FontSize(7).FontColor(Muted));
				text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
			});
		})).GeneratePdf();
	}

	private static void Fields(IContainer container, IReadOnlyList<ReceiptDocumentField> fields, bool omitEmpty = false)
	{
		var visible = fields.Where(field => !omitEmpty || !string.IsNullOrWhiteSpace(field.Value)).ToList();
		container.Table(table =>
		{
			table.ColumnsDefinition(columns => { columns.RelativeColumn(1.2f); columns.RelativeColumn(1.1f); columns.RelativeColumn(); });
			foreach (var field in visible)
				table.Cell().PaddingRight(12).PaddingBottom(10).Column(column =>
				{
					column.Item().Text(field.Label.ToUpperInvariant()).FontSize(7).Bold().FontColor(Muted);
					column.Item().PaddingTop(5).Text(string.IsNullOrWhiteSpace(field.Value) ? "—" : field.Value).SemiBold();
				});
		});
	}

	private static void LineTable(IContainer container, ReceiptDocumentModel model)
	{
		container.Table(table =>
		{
			table.ColumnsDefinition(columns =>
			{
				columns.ConstantColumn(22); columns.RelativeColumn(2.1f); columns.RelativeColumn(2.5f);
				columns.RelativeColumn(1.5f); columns.RelativeColumn(1.5f); columns.RelativeColumn(1.5f);
			});
			table.Header(header =>
			{
				var labels = model.IsRefund ? new[] { "#", "Fee head", "Installment", "Approved", "Refunded", "Pending" }
					: new[] { "#", "Fee head", "Installment", "Total fees", "Paid fees", "Pending fees" };
				for (var i = 0; i < labels.Length; i++)
				{
					var cell = header.Cell().Background(HeaderFill).PaddingVertical(11).PaddingHorizontal(5);
					if (i >= 3) cell = cell.AlignRight();
					cell.Text(labels[i]).FontSize(8).Bold();
				}
			});
			for (var index = 0; index < model.Lines.Count; index++)
			{
				var line = model.Lines[index];
				var values = new[] { (index + 1).ToString(CultureInfo.InvariantCulture), line.FeeHead, line.Installment,
					Money(line.TotalFees), Money(line.PaidFees), Money(line.PendingFees) };
				for (var i = 0; i < values.Length; i++)
				{
					var cell = table.Cell().Background(index % 2 == 0 ? "#F6F8F9" : Colors.White)
						.BorderBottom(.4f).BorderColor(Border).PaddingVertical(12).PaddingHorizontal(5);
					if (i >= 3) cell = cell.AlignRight();
					if (i == 1 && !line.PaidFees.HasValue)
						cell.Column(detail =>
						{
							detail.Item().Text(values[i]).FontSize(8);
							detail.Item().PaddingTop(4).Text($"Received: {Money(line.ReceivedAmount)}").FontSize(7).FontColor(Muted);
						});
					else cell.Text(values[i]).FontSize(8);
				}
			}
			var totals = new[] { "TOTAL", Money(Sum(model.Lines.Select(line => line.TotalFees))),
				Money(Sum(model.Lines.Select(line => line.PaidFees))), Money(Sum(model.Lines.Select(line => line.PendingFees))) };
			table.Cell().ColumnSpan(3).Background(HeaderFill).Padding(10).Text(totals[0]).FontSize(8).Bold();
			foreach (var total in totals.Skip(1)) table.Cell().Background(HeaderFill).PaddingVertical(10).PaddingHorizontal(5).AlignRight().Text(total).FontSize(8).Bold();
		});
	}

	private static decimal? Sum(IEnumerable<decimal?> values)
	{
		var items = values.ToList();
		return items.Count == 0 || items.Any(value => !value.HasValue) ? null : items.Sum(value => value!.Value);
	}
}
