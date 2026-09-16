using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Reports;

/// <summary>
/// Generates styled PDF reports for curriculum report types.
/// Uses QuestPDF Fluent API directly for a rich, professional layout.
/// Header (navy banner + title) and footer (date left, page X of Y right) are shared across all report types.
/// </summary>
public class AcademicPdfService : ITransientDependency
{
    private const string AccentColor = "#1E3A5F";
    private const string AccentLight = "#EEF2F7";
    private const string AltRowBg = "#F8FAFC";
    private const string BorderColor = "#CBD5E1";

    public static bool IsCurriculumReport(string reportType) =>
        reportType is AcademicReportTypes.ChapterProgressReport
            or AcademicReportTypes.SubjectWiseChapterCoverage
            or AcademicReportTypes.ChapterDescriptionSummary
            or AcademicReportTypes.TimelineReport
            or AcademicReportTypes.ClassWiseChapterAssignment
            or AcademicReportTypes.CurriculumCompletionReport
            or AcademicReportTypes.ChapterRevisionTracker;

    public async Task<byte[]> GenerateReportPdf(AcademicReportPreviewDto report)
    {
        return await Task.FromResult(CreateDocument(report).GeneratePdf());
    }

    public void WriteReportPdf(AcademicReportPreviewDto report, Stream destination)
    {
        CreateDocument(report).GeneratePdf(destination);
    }

    internal void WriteReportPdf(AcademicReportPreviewDto report, IEnumerable<Dictionary<string, string?>> rows,
        int rowCount, Stream destination) => CreateDocument(report, rows, rowCount).GeneratePdf(destination);

    private static IDocument CreateDocument(AcademicReportPreviewDto report,
        IEnumerable<Dictionary<string, string?>>? rows = null, int? rowCount = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontColor(Colors.Grey.Darken4));

                page.Header().Element(c => ComposeHeader(c, report));
                page.Content().Element(c => ComposeTable(c, report, rows ?? report.Rows, rowCount ?? report.Rows.Count));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    private static void ComposeHeader(IContainer container, AcademicReportPreviewDto report)
    {
        container.Background(AccentColor).Padding(14).Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Text(report.ReportType)
                    .FontSize(16).Bold().FontColor(Colors.White);
                left.Item().Text("Curriculum Report \u2022 School Management System")
                    .FontSize(8).FontColor("#93C5FD");
            });
            row.ConstantItem(180).AlignRight().AlignMiddle().Column(right =>
            {
                var istTime = DateTime.UtcNow.AddMinutes(330);
                right.Item().AlignRight().Text($"Generated: {istTime:dd MMM yyyy, hh:mm tt} IST")
                    .FontSize(7.5f).FontColor("#BFDBFE");
                right.Item().AlignRight().Text($"Records: {report.TotalCount}")
                    .FontSize(7.5f).FontColor("#BFDBFE");
            });
        });
    }

    private static void ComposeTable(IContainer container, AcademicReportPreviewDto report,
        IEnumerable<Dictionary<string, string?>> rows, int rowCount)
    {
        if (rowCount == 0)
        {
            container.Background(AccentLight).Padding(20).AlignCenter()
                .Text("No records found for the selected filters.")
                .FontSize(10).FontColor(Colors.Grey.Medium);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                foreach (var _ in report.Columns)
                    cols.RelativeColumn();
            });

            table.Header(header =>
            {
                foreach (var col in report.Columns)
                {
                    header.Cell()
                        .Background(AccentColor)
                        .BorderBottom(1.5f).BorderColor(AccentColor)
                        .PaddingHorizontal(8).PaddingVertical(7)
                        .Text(col.Label)
                        .FontSize(8).Bold().FontColor(Colors.White);
                }
            });

            var rowIndex = 0;
            foreach (var row in rows)
            {
                var bg = rowIndex % 2 == 1 ? AltRowBg : "#FFFFFF";
                foreach (var col in report.Columns)
                {
                    var value = row.TryGetValue(col.Id, out var v) ? v ?? string.Empty : string.Empty;
                    
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

                    table.Cell()
                        .Background(bg)
                        .BorderBottom(0.5f).BorderColor(BorderColor)
                        .PaddingHorizontal(8).PaddingVertical(5)
                        .Text(value)
                        .FontSize(8).FontColor(Colors.Grey.Darken4);
                }
                rowIndex++;
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            var istTime = DateTime.UtcNow.AddMinutes(330);
            row.RelativeItem().AlignLeft().Text($"Date: {istTime:dd MMM yyyy}")
                .FontSize(7).FontColor(Colors.Grey.Medium);
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
                text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                text.Span(" of ").FontSize(7).FontColor(Colors.Grey.Medium);
                text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
            });
        });
    }
}

