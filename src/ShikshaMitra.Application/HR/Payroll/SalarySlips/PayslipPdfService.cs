using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using System.IO;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfSections;
using static PayslipPdfFormatting;

public class PayslipPdfService : ITransientDependency
{
	public byte[] Generate(PayslipPdfModel model)
	{
		model = model with
		{
			Department = model.Presentation.ShowStaffDetails ? model.Department : null,
			Designation = model.Presentation.ShowStaffDetails ? model.Designation : null,
			DateOfJoining = model.Presentation.ShowStaffDetails ? model.DateOfJoining : null,
			UanNumber = model.Presentation.ShowStaffDetails ? model.UanNumber : null,
			EpfNumber = model.Presentation.ShowStaffDetails ? model.EpfNumber : null,
			InsuranceNumber = model.Presentation.ShowStaffDetails ? model.InsuranceNumber : null,
			BankName = model.Presentation.ShowBankDetails ? model.BankName : null,
			MaskedAccountNumber = model.Presentation.ShowBankDetails ? model.MaskedAccountNumber : null,
			IfscCode = model.Presentation.ShowBankDetails ? model.IfscCode : null,
			Earnings = model.Presentation.ShowEarningsBreakdown ? model.Earnings : new[] { new PayslipPdfLineItem("Total earnings", model.TotalEarnings) },
			Deductions = model.Presentation.ShowZeroDeductions ? model.Deductions : System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(model.Deductions, x => x.Amount != 0m))
		};
		PortablePdfFonts.EnsureRegistered();
		using var stream = new MemoryStream();

		Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size(PageSizes.A4);
				page.Margin(28);
				page.DefaultTextStyle(x => x.FontFamily(PortablePdfFonts.DefaultFamily, PortablePdfFonts.GujaratiFamily)
					.FontSize(9).FontColor(Colors.Grey.Darken4)
					.DisableFontFeature(FontFeatures.StandardLigatures));
				page.Header().Column(header =>
				{
					header.Item().ShowOnce().Element(c => ComposeHeader(c, model));
					header.Item().SkipOnce().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(6)
						.LogicalText($"{model.EmployeeCode} | {model.EmployeeName} | {model.PayPeriodDisplay}\n{model.SlipNumber}").FontSize(8);
				});

				page.Content().PaddingTop(10).Column(column =>
				{
					column.Spacing(model.Presentation.Layout == "Compact" ? 6 : 10);
					column.Item().Element(c => ComposeInfoBlocks(c, model));
					if (model.Presentation.ShowAttendance) column.Item().Element(c => ComposePayBasis(c, model));
					column.Item().Element(c => ComposeSalaryTables(c, model));
					column.Item().Element(c => ComposeNetPay(c, model));
				});

				page.Footer().Element(c => ComposeFooter(c, model));
			});
		}).WithMetadata(new DocumentMetadata { Language = "en-IN", Title = $"Salary slip {model.SlipNumber}" })
			.WithSettings(new DocumentSettings { PDFUA_Conformance = PDFUA_Conformance.PDFUA_1 })
			.GeneratePdf(stream);

		return stream.ToArray();
	}
}