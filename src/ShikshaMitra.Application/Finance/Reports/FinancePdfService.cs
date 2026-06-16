using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ShikshaMitra.Finance.Reports;

public class FinancePdfService : ITransientDependency
{
    public Task<byte[]> GenerateReportPdfAsync(FinanceReportPreviewDto data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().Text(data.ReportType).FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Generated on {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(10).FontColor(Colors.Grey.Medium);
                        col.Spacing(5);
                    });
                });

                page.Content().PaddingVertical(1, Unit.Centimetre).Element(content =>
                {
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
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).Text(value?.ToString() ?? "");
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
