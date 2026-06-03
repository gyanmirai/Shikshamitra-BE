using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfFormatting;
using static PayslipPdfTableBlocks;

internal static class PayslipPdfSections
{
	private const int SideBySideLineLimit = 10;

	public static void ComposeHeader(IContainer container, PayslipPdfModel model)
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
				("EPF No.", model.EpfNumber)
			}));
		});
	}

	public static void ComposePayBasis(IContainer container, PayslipPdfModel model)
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

	public static void ComposeSalaryTables(IContainer container, PayslipPdfModel model)
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

	public static void ComposeNetPay(IContainer container, PayslipPdfModel model)
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

	public static void ComposeFooter(IContainer container, PayslipPdfModel _)
	{
		container.Column(column =>
		{
			column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
			column.Item().PaddingTop(6).AlignCenter().Text("This is a computer-generated payslip and does not require a signature.").FontSize(8).FontColor(Colors.Grey.Darken1);
			column.Item().AlignCenter().Text("Confidential payroll document. Contact HR/payroll for any discrepancy.").FontSize(8).FontColor(Colors.Grey.Darken1);
		});
	}

}
