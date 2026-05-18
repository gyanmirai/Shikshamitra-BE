using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipPdfService : ITransientDependency
{
	private const int SideBySideLineLimit = 10;
	private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");

	public byte[] Generate(PayslipPdfModel model)
	{
		using var stream = new MemoryStream();

		Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(28);
				page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

				page.Content().Column(column =>
				{
					column.Spacing(12);
					column.Item().Element(c => ComposeHeader(c, model));
					column.Item().Element(c => ComposeInfoBlocks(c, model));
					column.Item().Element(c => ComposePayBasis(c, model));
					column.Item().Element(c => ComposeSalaryTables(c, model));
					column.Item().Element(c => ComposeNetPay(c, model));
				});

				page.Footer().Element(c => ComposeFooter(c, model));
			});
		}).GeneratePdf(stream);

		return stream.ToArray();
	}

	private static void ComposeHeader(IContainer container, PayslipPdfModel model)
	{
		container.Column(column =>
		{
			column.Item().Row(row =>
			{
				row.RelativeItem().Column(left =>
				{
					left.Item().Text(model.InstituteName).FontSize(18).Bold().FontColor("#0F172A");
					if (!string.IsNullOrWhiteSpace(model.InstituteAddress))
					{
						left.Item().Text(model.InstituteAddress).FontSize(8).FontColor(Colors.Grey.Darken1);
					}
				});

				row.ConstantItem(190).AlignRight().Column(right =>
				{
					right.Item().Text($"Payslip for {model.PayPeriodDisplay}").FontSize(14).Bold().FontColor("#1D4ED8");
					right.Item().Text($"Generated: {model.GeneratedAt:dd MMM yyyy}").FontSize(8).FontColor(Colors.Grey.Darken1);
					right.Item().Text($"Slip No: {model.SlipNumber}").FontSize(8).FontColor(Colors.Grey.Darken1);
				});
			});

			column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
		});
	}

	private static void ComposeInfoBlocks(IContainer container, PayslipPdfModel model)
	{
		container.Row(row =>
		{
			row.RelativeItem().Element(c => ComposeKeyValueBlock(c, "Employee Details", new[]
			{
				("Name", model.EmployeeName),
				("Employee Code", model.EmployeeCode),
				("Department", model.Department),
				("Designation", model.Designation),
				("Joining Date", model.DateOfJoining)
			}));

			row.ConstantItem(14);

			row.RelativeItem().Element(c => ComposeKeyValueBlock(c, "Payment & Statutory", new[]
			{
				("Bank", model.BankName),
				("Account", model.MaskedAccountNumber),
				("IFSC", model.IfscCode),
				("UAN", model.UanNumber),
				("EPF No.", model.EpfNumber)
			}));
		});
	}

	private static void ComposePayBasis(IContainer container, PayslipPdfModel model)
	{
		container.Border(1).BorderColor(Colors.Grey.Lighten2).Background("#F8FAFC").Padding(10).Column(column =>
		{
			column.Item().Text("Pay Basis").Bold().FontColor("#0F172A");
			column.Item().PaddingTop(6).Table(table =>
			{
				table.ColumnsDefinition(columns =>
				{
					columns.RelativeColumn();
					columns.RelativeColumn();
					columns.RelativeColumn();
					columns.RelativeColumn();
				});

				AddMetric(table, "Payable Days", FormatDays(model.PayableDays));
				AddMetric(table, "Payroll Basis Days", FormatDays(model.PayrollBasisDays));
				AddMetric(table, "LOP / Absent Days", FormatDays(model.LopDays));
				AddMetric(table, "Proration Factor", model.ProrationFactor.HasValue ? model.ProrationFactor.Value.ToString("0.####", CultureInfo.InvariantCulture) : "-");
				AddMetric(table, "Present Days", FormatDays(model.PresentDays));
				AddMetric(table, "Half Days", FormatDays(model.HalfWorkingDays));
				AddMetric(table, "Day Calculation", model.DayCalculationMode);
				AddMetric(table, "Pay Period", model.PayPeriodDisplay);
			});
		});
	}

	private static void ComposeSalaryTables(IContainer container, PayslipPdfModel model)
	{
		var shouldStackTables = model.Earnings.Count > SideBySideLineLimit ||
		                        model.Deductions.Count > SideBySideLineLimit;

		if (shouldStackTables)
		{
			container.Column(column =>
			{
				column.Spacing(10);
				column.Item().Element(c => ComposeLineTable(c, "Earnings", model.Earnings, model.TotalEarnings, "#DCFCE7", "#166534"));
				column.Item().Element(c => ComposeLineTable(c, "Deductions", model.Deductions, model.TotalDeductions, "#FFE4E6", "#991B1B"));
			});
			return;
		}

		container.Row(row =>
		{
			row.RelativeItem().Element(c => ComposeLineTable(c, "Earnings", model.Earnings, model.TotalEarnings, "#DCFCE7", "#166534"));
			row.ConstantItem(14);
			row.RelativeItem().Element(c => ComposeLineTable(c, "Deductions", model.Deductions, model.TotalDeductions, "#FFE4E6", "#991B1B"));
		});
	}

	private static void ComposeLineTable(IContainer container, string title, IReadOnlyList<PayslipPdfLineItem> lines, decimal total, string headerBg, string headerColor)
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

	private static void ComposeNetPay(IContainer container, PayslipPdfModel model)
	{
		container.Border(1).BorderColor("#BFDBFE").Background("#EFF6FF").Padding(12).Column(column =>
		{
			column.Item().Row(row =>
			{
				row.RelativeItem().Text("Net Payable").FontSize(12).Bold().FontColor("#1E3A8A");
				row.RelativeItem().AlignRight().Text(FormatCurrency(model.NetPay)).FontSize(15).Bold().FontColor("#1D4ED8");
			});
			column.Item().PaddingTop(4).Text(model.NetPayInWords).FontSize(9).FontColor("#1E3A8A");

			if (!string.IsNullOrWhiteSpace(model.Remarks))
			{
				column.Item().PaddingTop(8).Text($"Remarks: {model.Remarks}").FontSize(8).FontColor(Colors.Grey.Darken2);
			}
		});
	}

	private static void ComposeFooter(IContainer container, PayslipPdfModel _)
	{
		container.Column(column =>
		{
			column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
			column.Item().PaddingTop(6).AlignCenter().Text("This is a computer-generated payslip and does not require a signature.").FontSize(8).FontColor(Colors.Grey.Darken1);
			column.Item().AlignCenter().Text("Confidential payroll document. Contact HR/payroll for any discrepancy.").FontSize(8).FontColor(Colors.Grey.Darken1);
		});
	}

	private static void ComposeKeyValueBlock(IContainer container, string title, IEnumerable<(string Label, string? Value)> rows)
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

	private static void AddMetric(TableDescriptor table, string label, string? value)
	{
		table.Cell().PaddingBottom(6).Column(column =>
		{
			column.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken1);
			column.Item().Text(string.IsNullOrWhiteSpace(value) ? "-" : value).Bold().FontColor("#0F172A");
		});
	}

	private static IContainer HeaderCell(IContainer container, string background) =>
		container.Background(background).Border(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(6);

	private static IContainer BodyCell(IContainer container) =>
		container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(6);

	private static IContainer TotalCell(IContainer container, string background) =>
		container.Background(background).BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(6);

	private static string FormatCurrency(decimal amount) =>
		$"Rs. {amount.ToString("N2", IndianCulture)}";

	private static string FormatDays(decimal? days) =>
		days.HasValue ? days.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
}

public sealed record PayslipPdfModel(
	string InstituteName,
	string? InstituteAddress,
	string EmployeeName,
	string EmployeeCode,
	string? Department,
	string? Designation,
	string? DateOfJoining,
	string PayPeriodDisplay,
	string SlipNumber,
	DateTime GeneratedAt,
	decimal? PayrollBasisDays,
	decimal? PayableDays,
	decimal? PresentDays,
	decimal? HalfWorkingDays,
	decimal? LopDays,
	decimal? ProrationFactor,
	string? DayCalculationMode,
	string? BankName,
	string? MaskedAccountNumber,
	string? IfscCode,
	string? UanNumber,
	string? EpfNumber,
	IReadOnlyList<PayslipPdfLineItem> Earnings,
	IReadOnlyList<PayslipPdfLineItem> Deductions,
	decimal TotalEarnings,
	decimal TotalDeductions,
	decimal NetPay,
	string NetPayInWords,
	string? Remarks);

public sealed record PayslipPdfLineItem(string Name, decimal Amount);
