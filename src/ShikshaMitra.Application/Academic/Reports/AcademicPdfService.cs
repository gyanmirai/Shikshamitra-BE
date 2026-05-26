using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using IElement = ShikshaMitra.Common.PDF.Elements.IElement;

namespace ShikshaMitra.Academic.Reports;

public class AcademicPdfService(IPdfService pdfService) : ITransientDependency
{
    public async Task<byte[]> GenerateReportPdf(AcademicReportPreviewDto report)
    {
        var columns = report.Columns.Select(c => new TableElement.Column
        {
            Type = TableElement.Column.ColumnType.Relative,
            Size = 1
        }).ToList();

        var cells = new List<Cell>();

        // Header cells
        uint colIndex = 1;
        foreach (var col in report.Columns)
        {
            cells.Add(new Cell
            {
                Row = 1,
                Column = colIndex++,
                BackgroundColor = "#f3f4f6",
                Padding = 4,
                BorderWidth = 1,
                BorderColor = "#e5e7eb",
                Element = new TextElement
                {
                    Text = col.Label ?? string.Empty,
                    FontWeight = FontWeight.Bold,
                    FontSize = 10
                }
            });
        }

        // Data cells
        uint rowIndex = 2;
        foreach (var row in report.Rows.Take(200)) // Max limit to avoid exploding PDF in size randomly
        {
            colIndex = 1;
            foreach (var col in report.Columns)
            {
                var textValue = row.TryGetValue(col.Id, out var val) ? val ?? string.Empty : string.Empty;
                cells.Add(new Cell
                {
                    Row = rowIndex,
                    Column = colIndex++,
                    Padding = 4,
                    BorderWidth = 1,
                    BorderColor = "#e5e7eb",
                    Element = new TextElement
                    {
                        Text = textValue,
                        FontSize = 9
                    }
                });
            }
            rowIndex++;
        }

        var table = new TableElement
        {
            Columns = columns,
            Cells = cells,
            BorderWidth = 1
        };

        var document = new PDFDocument
        {
            Page = new PageSpecs
            {
                BackgroundColor = "#ffffff",
                Margin = 20,
                MarginUnit = Unit.Point,
                Orientation = PageSpecs.PageOrientation.Landscape,
                PageSize = "A4"
            },
            Header = new TextElement
            {
                Text = report.ReportType,
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                Alignment = TextAlignment.Center,
                Padding = 10,
                PaddingUnit = Unit.Point
            },
            Content = table
        };

        return await pdfService.GeneratePdfAsBytes(document);
    }
}
