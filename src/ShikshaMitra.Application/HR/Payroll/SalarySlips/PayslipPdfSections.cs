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
				if (model.LogoBytes != null) row.ConstantItem(52).PaddingRight(8).Height(44).Image(model.LogoBytes).FitArea();
				row.RelativeItem().Column(left =>
				{
					left.Item().LogicalText(model.InstituteName).FontSize(17).Bold().FontColor(Ink);
					if (!string.IsNullOrWhiteSpace(model.InstituteAddress))
					{
						left.Item().PaddingTop(2).LogicalText(model.InstituteAddress).FontSize(8).FontColor(Muted);
					}
					left.Item().PaddingTop(2).LogicalText(model.IsPreview ? "Preview salary statement" : "Official salary statement").FontSize(8).FontColor(Muted);
				});

				row.ConstantItem(190).AlignRight().Column(right =>
				{
					right.Item().AlignRight().Border(1).BorderColor(Ink).PaddingVertical(3).PaddingHorizontal(8)
						.LogicalText(NormalizeStatus(model.DocumentLabel)).FontSize(8).Bold().FontColor(Ink);
					right.Item().PaddingTop(6).LogicalText(model.PayPeriodDisplay).FontSize(14).Bold().FontColor(Ink);
					right.Item().PaddingTop(3).LogicalText($"Generated On: {model.GeneratedAt:dd MMM yyyy}").FontSize(8).FontColor(Muted);
				});
			});
			column.Item().PaddingTop(5).AlignRight().LogicalText($"Slip No: {model.SlipNumber}").FontSize(8).FontColor(Muted);

			column.Item().PaddingTop(10).AlignCenter().LogicalText("Salary Slip").FontSize(16).Bold().FontColor(Ink);
			column.Item().PaddingTop(8).LineHorizontal(2).LineColor(Ink);
			if (model.IsPreview)
			{
				column.Item().PaddingTop(8).Border(1).BorderColor("#D6D3D1").Background("#FAFAF9").Padding(7)
					.LogicalText("Preview payslip only. Finalize/lock payroll to issue the official salary slip.")
					.FontSize(8).Bold().FontColor(Ink);
			}
		});
	}

	public static void ComposeInfoBlocks(IContainer container, PayslipPdfModel model)
	{
		var hasBankDetails = new[] { model.BankName, model.MaskedAccountNumber, model.IfscCode }
			.Any(value => !string.IsNullOrWhiteSpace(value));
		var hasStatutoryDetails = new[] { model.UanNumber, model.EpfNumber, model.InsuranceNumber }
			.Any(value => !string.IsNullOrWhiteSpace(value));
		container.Row(row =>
		{
			row.RelativeItem().Element(c => ComposeKeyValueBlock(c, "Staff Details", new[]
			{
				("Name", model.EmployeeName),
				("Staff Code", model.EmployeeCode),
				("Department", model.Department),
				("Designation", model.Designation),
				("Joining Date", model.DateOfJoining)
			}));

			if (!hasBankDetails && !hasStatutoryDetails) return;
			row.ConstantItem(14);

			var title = hasBankDetails && hasStatutoryDetails ? "Payment & Statutory"
				: hasBankDetails ? "Payment details" : "Statutory details";
			row.RelativeItem().Element(c => ComposeKeyValueBlock(c, title, new[]
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
			column.Item().LogicalText("Earnings and attendance").Bold().FontColor(Ink);
			if (model.FullGrossSalary.HasValue)
				column.Item().PaddingTop(4).LogicalText($"Full-month earnings: {FormatCurrency(model.FullGrossSalary.Value)}").FontSize(8);
			column.Item().PaddingTop(4).LogicalText($"CL: {FormatDays(model.CasualLeaveDays)}  |  Leave without pay: {FormatDays(model.UnpaidLeaveDays)}").FontSize(8);
			column.Item().PaddingTop(4).LogicalText($"Payable days: {FormatDays(model.PayableDays)}  |  Payroll days: {FormatDays(model.PayrollBasisDays)}  |  Non-payable days: {FormatDays(model.LopDays)}").FontSize(8);
		});
	}
	public static void ComposeSalaryTables(IContainer container, PayslipPdfModel model)
	{
		var shouldStackTables = model.Presentation.Layout == "Detailed" && model.Earnings.Any(x => x.FullMonthAmount.HasValue) || model.Earnings.Count > SideBySideLineLimit ||
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
					left.Item().LogicalText("Net Payable").FontSize(11).Bold().FontColor(Ink);
					left.Item().PaddingTop(2).LogicalText("Final amount payable after all deductions.").FontSize(8).FontColor(Muted);
				});
				row.RelativeItem().AlignRight().LogicalText(FormatCurrency(model.NetPay)).FontSize(14).Bold().FontColor(Ink);
			});
			column.Item().PaddingTop(6).LogicalText(model.NetPayInWords).FontSize(9).FontColor(Ink);

			if (!string.IsNullOrWhiteSpace(model.Remarks))
			{
				column.Item().PaddingTop(8).LogicalText($"Remarks: {model.Remarks}").FontSize(8).FontColor(Muted);
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
				column.Item().PaddingTop(6).AlignCenter().LogicalText("This is a preview payslip and is not an official salary slip.").FontSize(8).Bold().FontColor(Ink);
			}
			if (!model.IsPreview)
				column.Item().PaddingTop(6).LogicalText("Employer / pay-in-charge signature: ____________________").FontSize(8).FontColor(Ink);
			column.Item().AlignCenter().LogicalText("Confidential payroll document. Contact HR/payroll for any discrepancy.").FontSize(8).FontColor(Muted);
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
			column.Item().LogicalText(label).FontSize(8).Bold().FontColor(Muted);
			column.Item().PaddingTop(3).LogicalText(value).FontSize(11).Bold().FontColor(Ink);
		});
	}

	private static void ComposePayBasisMetric(IContainer container, string label, string value, string helper)
	{
		container.Border(1).BorderColor(Border).Background(Panel).Padding(7).Column(column =>
		{
			column.Item().LogicalText(label).FontSize(7).Bold().FontColor(Muted);
			column.Item().PaddingTop(4).LogicalText(value).FontSize(12).Bold().FontColor(Ink);
			column.Item().PaddingTop(3).LogicalText(helper).FontSize(7).FontColor(Muted);
		});
	}

	private static void ComposeCompactMetric(IContainer container, string label, string? value)
	{
		container.Border(1).BorderColor(Border).Padding(7).Column(column =>
		{
			column.Item().LogicalText(label).FontSize(7).Bold().FontColor(Muted);
			column.Item().PaddingTop(2).LogicalText(string.IsNullOrWhiteSpace(value) ? "-" : value).FontSize(9).Bold().FontColor(Ink);
		});
	}

}