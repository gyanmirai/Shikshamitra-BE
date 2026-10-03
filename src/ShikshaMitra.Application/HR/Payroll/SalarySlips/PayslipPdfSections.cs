using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfFormatting;
using static PayslipPdfTableBlocks;

internal static class PayslipPdfSections
{
	private const int SideBySideLineLimit = 10;
	private const string Ink = "#111827";
	private const string Muted = "#6B7280";
	private const string Border = "#E5E7EB";
	private const string Panel = "#F9FAFB";

	public static void ComposeHeader(IContainer container, PayslipPdfModel model)
	{
		container.Column(column =>
		{
			column.Item().Row(row =>
			{
				row.RelativeItem().Column(left =>
				{
					left.Item().Text(model.InstituteName).FontSize(17).Bold().FontColor(Ink);
					if (!string.IsNullOrWhiteSpace(model.InstituteAddress))
					{
						left.Item().PaddingTop(2).Text(model.InstituteAddress).FontSize(8).FontColor(Muted);
					}
					left.Item().PaddingTop(2).Text(model.IsPreview ? "Preview salary statement" : "Official salary statement").FontSize(8).FontColor(Muted);
				});

				row.ConstantItem(190).AlignRight().Column(right =>
				{
					right.Item().AlignRight().Border(1).BorderColor(Ink).PaddingVertical(3).PaddingHorizontal(8)
						.Text(NormalizeStatus(model.DocumentLabel)).FontSize(8).Bold().FontColor(Ink);
					right.Item().PaddingTop(6).Text(model.PayPeriodDisplay).FontSize(14).Bold().FontColor(Ink);
					right.Item().PaddingTop(3).Text($"Generated On: {model.GeneratedAt:dd MMM yyyy}").FontSize(8).FontColor(Muted);
				});
			});
			column.Item().PaddingTop(5).AlignRight().Text($"Slip No: {model.SlipNumber}").FontSize(8).FontColor(Muted);

			column.Item().PaddingTop(10).AlignCenter().Text("Salary Slip").FontSize(16).Bold().FontColor(Ink);
			column.Item().PaddingTop(8).LineHorizontal(2).LineColor(Ink);
			if (model.IsPreview)
			{
				column.Item().PaddingTop(8).Border(1).BorderColor("#D6D3D1").Background("#FAFAF9").Padding(7)
					.Text("Preview payslip only. Finalize/lock payroll to issue the official salary slip.")
					.FontSize(8).Bold().FontColor(Ink);
			}
		});
	}

	public static void ComposeInfoBlocks(IContainer container, PayslipPdfModel model)
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
				("EPF No.", model.EpfNumber),
				("ESIC insurance number", model.InsuranceNumber)
			}));
		});
	}

	public static void ComposeSummary(IContainer container, PayslipPdfModel model)
	{
		container.Row(row =>
		{
			row.RelativeItem().Element(c => ComposeSummaryMetric(c, "Total Earnings", FormatCurrency(model.TotalEarnings)));
			row.ConstantItem(8);
			row.RelativeItem().Element(c => ComposeSummaryMetric(c, "Total Deductions", FormatCurrency(model.TotalDeductions)));
			row.ConstantItem(8);
			row.RelativeItem().Element(c => ComposeSummaryMetric(c, "Net Payable", FormatCurrency(model.NetPay)));
		});
	}

	public static void ComposePayBasis(IContainer container, PayslipPdfModel model)
	{
		container.Border(1).BorderColor(Border).Padding(10).Column(column =>
		{
			column.Item().Text("Earnings and attendance").Bold().FontColor(Ink);
			if (model.FullGrossSalary.HasValue)
				column.Item().PaddingTop(4).Text($"Full-month earnings: {FormatCurrency(model.FullGrossSalary.Value)}").FontSize(8);
			column.Item().PaddingTop(4).Text($"Payable days: {FormatDays(model.PayableDays)}  |  Payroll days: {FormatDays(model.PayrollBasisDays)}  |  Unpaid days: {FormatDays(model.LopDays)}").FontSize(8);
		});
	}
	public static void ComposeSalaryTables(IContainer container, PayslipPdfModel model)
	{
		var shouldStackTables = model.Earnings.Any(x => x.FullMonthAmount.HasValue) || model.Earnings.Count > SideBySideLineLimit ||
		                        model.Deductions.Count > SideBySideLineLimit;

		if (shouldStackTables)
		{
			container.Column(column =>
			{
				column.Spacing(8);
				column.Item().Element(c => ComposeLineTable(c, "Earnings", model.Earnings, model.TotalEarnings));
				column.Item().Element(c => ComposeLineTable(c, "Deductions", model.Deductions, model.TotalDeductions));
			});
			return;
		}

		container.Row(row =>
		{
			row.RelativeItem().Element(c => ComposeLineTable(c, "Earnings", model.Earnings, model.TotalEarnings));
			row.ConstantItem(12);
			row.RelativeItem().Element(c => ComposeLineTable(c, "Deductions", model.Deductions, model.TotalDeductions));
		});
	}

	public static void ComposeNetPay(IContainer container, PayslipPdfModel model)
	{
		container.Border(1).BorderColor(Border).Background(Panel).Padding(10).Column(column =>
		{
			column.Item().Row(row =>
			{
				row.RelativeItem().Column(left =>
				{
					left.Item().Text("Net Payable").FontSize(11).Bold().FontColor(Ink);
					left.Item().PaddingTop(2).Text("Final amount payable after all deductions.").FontSize(8).FontColor(Muted);
				});
				row.RelativeItem().AlignRight().Text(FormatCurrency(model.NetPay)).FontSize(14).Bold().FontColor(Ink);
			});
			column.Item().PaddingTop(6).Text(model.NetPayInWords).FontSize(9).FontColor(Ink);

			if (!string.IsNullOrWhiteSpace(model.Remarks))
			{
				column.Item().PaddingTop(8).Text($"Remarks: {model.Remarks}").FontSize(8).FontColor(Muted);
			}
		});
	}

	public static void ComposeFooter(IContainer container, PayslipPdfModel model)
	{
		container.Column(column =>
		{
			column.Item().LineHorizontal(1).LineColor(Border);
			if (model.IsPreview)
			{
				column.Item().PaddingTop(6).AlignCenter().Text("This is a preview payslip and is not an official salary slip.").FontSize(8).Bold().FontColor(Ink);
			}
			if (!model.IsPreview)
				column.Item().PaddingTop(6).Text("Employer / pay-in-charge signature: ____________________").FontSize(8).FontColor(Ink);
			column.Item().AlignCenter().Text("Confidential payroll document. Contact HR/payroll for any discrepancy.").FontSize(8).FontColor(Muted);
			column.Item().AlignRight().Text(text =>
			{
				text.DefaultTextStyle(x => x.FontSize(7).FontColor(Muted));
				text.CurrentPageNumber();
				text.Span(" / ");
				text.TotalPages();
			});
		});
	}

	private static string NormalizeStatus(string? status)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return "STATUS PENDING";
		}

		return status.Trim().ToUpperInvariant();
	}

	private static void ComposeSummaryMetric(IContainer container, string label, string value)
	{
		container.Border(1).BorderColor(Border).Padding(8).Column(column =>
		{
			column.Item().Text(label).FontSize(8).Bold().FontColor(Muted);
			column.Item().PaddingTop(3).Text(value).FontSize(11).Bold().FontColor(Ink);
		});
	}

	private static void ComposePayBasisMetric(IContainer container, string label, string value, string helper)
	{
		container.Border(1).BorderColor(Border).Background(Panel).Padding(7).Column(column =>
		{
			column.Item().Text(label).FontSize(7).Bold().FontColor(Muted);
			column.Item().PaddingTop(4).Text(value).FontSize(12).Bold().FontColor(Ink);
			column.Item().PaddingTop(3).Text(helper).FontSize(7).FontColor(Muted);
		});
	}

	private static void ComposeCompactMetric(IContainer container, string label, string? value)
	{
		container.Border(1).BorderColor(Border).Padding(7).Column(column =>
		{
			column.Item().Text(label).FontSize(7).Bold().FontColor(Muted);
			column.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(9).Bold().FontColor(Ink);
		});
	}

}
