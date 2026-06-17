using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Common.PDF;
using ShikshaMitra.Common.PDF.Elements;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using IElement = ShikshaMitra.Common.PDF.Elements.IElement;

namespace ShikshaMitra.HR.Reports;

public class HRPdfService(IPdfService pdfService) : ITransientDependency
{
	public async Task<byte[]> GenerateReportPdf(HrReportPreviewDto report)
	{
		var header = new RowElement
		{
			Elements = report.Columns.Select(column => (IElement)new TextElement
			{
				Text = column.Label,
				FontWeight = FontWeight.Bold,
				Padding = 4,
				PaddingUnit = Unit.Point
			}).ToList(),
			Spacing = 8
		};

		var rows = report.Rows.Take(500).Select(row => (IElement)new RowElement
		{
			Elements = report.Columns.Select(column => {
                var value = row.TryGetValue(column.Id, out var v) ? v ?? string.Empty : string.Empty;
                if (!string.IsNullOrEmpty(value))
                {
                    if (value.Contains("<") && value.Contains(">"))
                    {
                        value = Regex.Replace(value, "<[^>]*>", string.Empty);
                    }
                    if (value.Contains("&"))
                    {
                        value = WebUtility.HtmlDecode(value);
                    }
                }
                return (IElement)new TextElement
                {
                    Text = value,
                    Padding = 4,
                    PaddingUnit = Unit.Point
                };
            }).ToList(),
			Spacing = 8
		});

		var document = new PDFDocument
		{
			Page = new PageSpecs
			{
				BackgroundColor = "#ffffff",
				Margin = 20,
				MarginUnit = Unit.Point
			},
			Header = new TextElement
			{
				Text = report.ReportType,
				FontSize = 20,
				FontWeight = FontWeight.Bold,
				Alignment = TextAlignment.Center,
				Padding = 10,
				PaddingUnit = Unit.Point
			},
			Content = new ColumnElement
			{
				Elements = new[] { (IElement)header }.Concat(rows).ToList(),
				Spacing = 12
			}
		};

		return await pdfService.GeneratePdfAsBytes(document);
	}
}
