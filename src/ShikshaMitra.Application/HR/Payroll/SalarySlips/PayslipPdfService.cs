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
					column.Spacing(10);
					column.Item().Element(c => ComposeInfoBlocks(c, model));
					column.Item().Element(c => ComposePayBasis(c, model));
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