using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.IO;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

using static PayslipPdfSections;

public class PayslipPdfService : ITransientDependency
{
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
				page.Header().Column(header =>
				{
					header.Item().ShowOnce().Element(c => ComposeHeader(c, model));
					header.Item().SkipOnce().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(6)
						.Text($"{model.EmployeeCode} | {model.EmployeeName} | {model.PayPeriodDisplay}\n{model.SlipNumber}").FontSize(8);
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
		}).GeneratePdf(stream);

		return stream.ToArray();
	}
}
