using System;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

/// <summary>
/// Generates PDF receipts using QuestPDF.
/// Selects the appropriate layout based on <see cref="FeeReceiptKind"/>.
/// </summary>
public class FeeReceiptPdfGenerator : IFeeReceiptPdfGenerator, ITransientDependency
{
    public Task<byte[]> GenerateAsync(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = receipt.ReceiptKind switch
        {
            FeeReceiptKind.FinalFee          => GenerateFinalFeeReceipt(receipt, student, academicYear),
            FeeReceiptKind.ProvisionalFee    => GenerateProvisionalFeeReceipt(receipt, student, academicYear),
            FeeReceiptKind.ProvisionalCharge => GenerateProvisionalChargeReceipt(receipt, student, academicYear),
            _                               => GenerateFinalFeeReceipt(receipt, student, academicYear),
        };

        return Task.FromResult(bytes);
    }

    // ── FinalFee template ─────────────────────────────────────────────────────

    private static byte[] GenerateFinalFeeReceipt(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(20, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(c => BuildHeader(c, "PAYMENT RECEIPT", receipt, Colors.Green.Medium));
                page.Content().Element(c => BuildContent(c, receipt, student, academicYear, isFinal: true));
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    // ── ProvisionalFee template ───────────────────────────────────────────────

    private static byte[] GenerateProvisionalFeeReceipt(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(20, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(c => BuildHeader(c, "PROVISIONAL RECEIPT (KACHI)", receipt, Colors.Orange.Medium));
                page.Content().Column(col =>
                {
                    col.Item().Element(c => BuildContent(c, receipt, student, academicYear, isFinal: false));
                    col.Item().PaddingTop(5).Text(
                        "* This is a provisional receipt subject to realisation of the instrument. " +
                        "A final receipt will be issued upon bank clearance.")
                        .FontColor(Colors.Red.Medium).FontSize(8).Italic();
                });
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    // ── ProvisionalCharge template ────────────────────────────────────────────

    private static byte[] GenerateProvisionalChargeReceipt(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(20, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(c => BuildHeader(c, "CHARGE RECEIPT (KACHI)", receipt, Colors.Blue.Medium));
                page.Content().Element(c => BuildContent(c, receipt, student, academicYear, isFinal: false, isCharge: true));
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    // ── Shared content builders ───────────────────────────────────────────────

    private static void BuildHeader(IContainer container, string title, FeePaymentReceipt receipt, string accentColor)
    {
        container.Column(col =>
        {
            col.Item().BorderBottom(1).BorderColor(accentColor).PaddingBottom(6).Row(row =>
            {
                row.RelativeItem().Text(title)
                    .Bold().FontSize(16).FontColor(accentColor);

                row.AutoItem().AlignRight().Column(rc =>
                {
                    rc.Item().Text($"Receipt No: {receipt.ReceiptNumber}").Bold();
                    rc.Item().Text($"Date: {receipt.ReceiptDate:dd-MMM-yyyy}");
                });
            });
        });
    }

    private static void BuildContent(
        IContainer container,
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        bool isFinal,
        bool isCharge = false)
    {
        var studentName = student != null
            ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
            : "—";

        container.PaddingTop(10).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                void AddRow(string label1, string val1, string label2, string val2)
                {
                    table.Cell().PaddingBottom(4).Text(label1).SemiBold();
                    table.Cell().PaddingBottom(4).Text(val1);
                    table.Cell().PaddingBottom(4).Text(label2).SemiBold();
                    table.Cell().PaddingBottom(4).Text(val2);
                }

                AddRow("Student Name", studentName,
                    "GR Number", student?.GrNo?.ToString() ?? "—");
                AddRow("Academic Year", academicYear?.Name ?? "—",
                    "Receipt Type", isCharge ? "Charge Receipt" : (isFinal ? "Final Fee Receipt" : "Provisional Fee Receipt"));
                AddRow("Amount Paid", $"\u20B9 {receipt.TotalPaidAmount:N2}",
                    "Transaction ID", receipt.FeeCollectionTransactionId.ToString()[..8] + "...");
            });

            col.Item().PaddingTop(12).Background(Colors.Grey.Lighten3).Padding(8).Text(
                $"Total Amount: \u20B9 {receipt.TotalPaidAmount:N2}")
                .FontSize(13).Bold();

            if (!isFinal && !isCharge)
            {
                col.Item().PaddingTop(6).Text(
                    "Note: Final receipt will be issued upon clearance of the instrument.")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
            }
        });
    }

    private static void BuildFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("Authorised Signatory").AlignLeft();
            row.RelativeItem().Text("School Seal").AlignCenter();
            row.RelativeItem().AlignRight()
                .Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
        });
    }
}
