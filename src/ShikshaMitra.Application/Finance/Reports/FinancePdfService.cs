using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;

namespace ShikshaMitra.Finance.Reports;

public class FinancePdfService : ITransientDependency
{
	public Task<byte[]> GenerateReportPdfAsync(FinanceReportPreviewDto data)
	{
		QuestPDF.Settings.License = LicenseType.Community;
		PortablePdfFonts.EnsureRegistered();

		var document = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.Size((data.Columns.Count > 11 ? PageSizes.A3 : PageSizes.A4).Landscape());
				page.Margin(1, Unit.Centimetre);
				page.PageColor(Colors.White);
				page.DefaultTextStyle(x => x.FontSize(data.Columns.Count > 11 ? 8 : 10)
					.FontFamily(PortablePdfFonts.DefaultFamily, PortablePdfFonts.GujaratiFamily));

				page.Header().Element(header =>
				{
					header.Column(col =>
					{
						col.Item().Text(data.ReportType).FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);

						var istTime = DateTime.UtcNow.AddMinutes(330);
						col.Item().Text($"Generated on {istTime:dd MMM yyyy HH:mm} IST").FontSize(10).FontColor(Colors.Grey.Medium);

						col.Spacing(5);
					});
				});

				page.Content().PaddingVertical(1, Unit.Centimetre).Element(content =>
				{
					if (data.Rows.Count == 0 || data.Columns.Count == 0)
					{
						content.Text("No data found for the selected filters.");
						return;
					}
					content.Table(table =>
					{
						// Define columns
						table.ColumnsDefinition(columns =>
						{
							foreach (var col in data.Columns)
							{
								columns.RelativeColumn();
							}
						});

						// Header row
						table.Header(header =>
						{
							foreach (var col in data.Columns)
							{
								header.Cell().BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(5).Text(col.Label).SemiBold();
							}
						});

						// Data rows
						foreach (var row in data.Rows)
						{
							foreach (var col in data.Columns)
							{
								var value = row.TryGetValue(col.Id, out var val) ? val : "";
								var cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
								if (col.Kind is "money" or "number")
									cell = cell.AlignRight();
								cell.Text(value ?? "");
							}
						}
					});
				});

				page.Footer().AlignCenter().Text(x =>
				{
					x.Span("Page ");
					x.CurrentPageNumber();
					x.Span(" of ");
					x.TotalPages();
				});
			});
		});

		return Task.FromResult(document.GeneratePdf());
	}
}
