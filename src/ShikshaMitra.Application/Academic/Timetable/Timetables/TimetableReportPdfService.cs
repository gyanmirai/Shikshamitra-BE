using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Timetable.Timetables;

public sealed record TimetableReportPdfModel
{
    public string InstituteName { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? ContactNumber { get; init; }
    public byte[]? LogoBytes { get; init; }
    public byte[]? BannerBytes { get; init; }
    public string StandardName { get; init; } = string.Empty;
    public string DivisionName { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    /// <summary>Days in display order. Key = day name, Value = ordered slot list.</summary>
    public List<string> Days { get; init; } = [];
    /// <summary>All unique slots (rows). Key = (StartTime, EndTime), Value = slot info per day.</summary>
    public List<TimetablePdfSlotRow> SlotRows { get; init; } = [];
}

public sealed record TimetablePdfSlotRow
{
    public int Sequence { get; init; }
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
    public bool IsBreak { get; init; }
    /// <summary>Key = day name, Value = cell content lines (subject, teacher).</summary>
    public Dictionary<string, TimetablePdfCell> Cells { get; init; } = [];
}

public sealed record TimetablePdfCell
{
    public List<string> SubjectNames { get; init; } = [];
    public List<string> TeacherNames { get; init; } = [];
}

public class TimetableReportPdfService : ITransientDependency
{
    private static readonly string AccentColor = "#C0392B";
    private static readonly string HeaderBg = "#FDF2F2";
    private static readonly string BreakBg = "#F5F5F5";
    private static readonly string BorderColor = "#D5D8DC";

    public byte[] Generate(TimetableReportPdfModel model)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken4));

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Element(c => ComposeHeader(c, model));
                    column.Item().Element(c => ComposeReportTitle(c, model));
                    column.Item().Element(c => ComposeTimetableTable(c, model));
                });

                page.Footer().Element(c => ComposeFooter(c));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, TimetableReportPdfModel model)
    {
        if (model.BannerBytes is { Length: > 0 })
        {
            container.MaxHeight(85).Image(model.BannerBytes).FitArea();
            return;
        }
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Logo placeholder (left)
                row.ConstantItem(72).Height(72).AlignMiddle().AlignCenter().Element(logoContainer =>
                {
                    if (model.LogoBytes is { Length: > 0 })
                    {
                        logoContainer.Image(model.LogoBytes).FitArea();
                    }
                });

                // School name + address (center)
                row.RelativeItem().PaddingHorizontal(12).AlignMiddle().Column(nameCol =>
                {
                    nameCol.Item().AlignCenter().Text(model.InstituteName)
                        .FontSize(20).Bold().FontColor(AccentColor);
                    if (!string.IsNullOrWhiteSpace(model.Address))
                    {
                        nameCol.Item().AlignCenter().Text(model.Address)
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    }
                    if (!string.IsNullOrWhiteSpace(model.ContactNumber))
                    {
                        nameCol.Item().AlignCenter().Text($"Contact: {model.ContactNumber}")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    }
                });

                // Right spacer to balance logo
                row.ConstantItem(72);
            });

            col.Item().PaddingTop(8).LineHorizontal(2).LineColor(AccentColor);
        });
    }

    private static void ComposeReportTitle(IContainer container, TimetableReportPdfModel model)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text("Class Timetable")
                .FontSize(13).Bold().FontColor(Colors.Grey.Darken4);
            col.Item().AlignCenter().Text($"Class: {model.StandardName} — Division: {model.DivisionName}")
                .FontSize(9).FontColor(Colors.Grey.Darken2);
        });
    }

    private static void ComposeTimetableTable(IContainer container, TimetableReportPdfModel model)
    {
        if (model.SlotRows.Count == 0 || model.Days.Count == 0)
        {
            container.Text("No timetable data available.").FontColor(Colors.Grey.Medium);
            return;
        }

        container.Table(table =>
        {
            // Columns: Time slot label + one per day
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(80); // Time column
                foreach (var _ in model.Days)
                    cols.RelativeColumn();
            });

            // Header row
            table.Header(header =>
            {
                header.Cell().Background(HeaderBg).Border(0.5f).BorderColor(BorderColor).Padding(5).Text("Time").Bold();
                foreach (var day in model.Days)
                {
                    header.Cell().Background(HeaderBg).Border(0.5f).BorderColor(BorderColor).Padding(5).AlignCenter().Text(day).Bold();
                }
            });

            // Data rows
            foreach (var slot in model.SlotRows)
            {
                var timeBg = slot.IsBreak ? BreakBg : "#FAFAFA";

                // Time label cell
                table.Cell().Background(timeBg).Border(0.5f).BorderColor(BorderColor).Padding(4).Column(timeCol =>
                {
                    timeCol.Item().Text(slot.StartTime).Bold().FontSize(7);
                    timeCol.Item().Text(slot.EndTime).FontSize(7).FontColor(Colors.Grey.Darken1);
                    if (slot.IsBreak)
                        timeCol.Item().Text("BREAK").FontSize(7).Italic().FontColor(Colors.Grey.Medium);
                });

                foreach (var day in model.Days)
                {
                    if (slot.IsBreak)
                    {
                        table.Cell().Background(BreakBg).Border(0.5f).BorderColor(BorderColor).Padding(4)
                            .AlignCenter().AlignMiddle()
                            .Text("—").FontColor(Colors.Grey.Medium);
                    }
                    else if (slot.Cells.TryGetValue(day, out var cell) && cell.SubjectNames.Count > 0)
                    {
                        table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(4).Column(cellCol =>
                        {
                            for (var i = 0; i < cell.SubjectNames.Count; i++)
                            {
                                if (i > 0)
                                    cellCol.Item().PaddingTop(2).LineHorizontal(0.5f).LineColor(BorderColor);

                                cellCol.Item().Text(cell.SubjectNames[i])
                                    .Bold().FontSize(7.5f).FontColor(Colors.Grey.Darken4);
                                if (i < cell.TeacherNames.Count && !string.IsNullOrWhiteSpace(cell.TeacherNames[i]))
                                {
                                    cellCol.Item().Text(cell.TeacherNames[i])
                                        .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                                }
                            }
                        });
                    }
                    else
                    {
                        table.Cell().Border(0.5f).BorderColor(BorderColor).Padding(4).Text(string.Empty);
                    }
                }
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft()
                .Text(text =>
                {
                    text.Span($"Generated: {DateTime.Now:dd MMM yyyy, hh:mm tt}")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });

            row.RelativeItem().AlignRight()
                .Text(text =>
                {
                    text.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                    text.Span(" of ").FontSize(7).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                });
        });
    }

}
