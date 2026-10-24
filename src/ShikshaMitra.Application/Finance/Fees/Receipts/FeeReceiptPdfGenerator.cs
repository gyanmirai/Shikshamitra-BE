using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Common.PDF;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

/// <summary>Printable receipt evidence. Payment methods come from the original collection.</summary>
public class FeeReceiptPdfGenerator(
    IReadOnlyRepository<FinanceFeeAccount, Guid>? accounts = null,
    IReadOnlyRepository<PaymentMethodRecord, Guid>? paymentRecords = null,
    IReadOnlyRepository<FeeCollectionDueFeeAllocation, Guid>? allocations = null,
    IFeePaymentReceiptRepository? receipts = null,
    ICurrentTenant? currentTenant = null) : IFeeReceiptPdfGenerator, ITransientDependency
{
    private static string Money(decimal value) => $"INR {value.ToString("N2", CultureInfo.GetCultureInfo("en-IN"))}";

    public async Task<byte[]> GenerateAsync(FeePaymentReceipt receipt, Student? student,
        AcademicYear? academicYear, CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        PortablePdfFonts.EnsureRegistered();
        var applicantName = receipt.FinanceFeeAccountId.HasValue && accounts != null
            ? (await accounts.GetAsync(receipt.FinanceFeeAccountId.Value, cancellationToken: cancellationToken)).ApplicantName : null;
        List<PaymentMethodRecord> payments = paymentRecords == null ? [] : await paymentRecords.GetListAsync(
            p => p.FeeRecordMetadataId == receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
        List<FeeCollectionDueFeeAllocation> feeLines = allocations == null || receipt.ReceiptScope == FeeReceiptScope.Charge ? [] : await allocations.GetListAsync(
            a => a.FeeRecordMetadataId == receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
        var predecessor = receipt.LinkedProvisionalReceiptId.HasValue && receipts != null
            ? await receipts.FindAsync(receipt.LinkedProvisionalReceiptId.Value, cancellationToken: cancellationToken) : null;
        var provisional = FeeReceiptPresentation.IsProvisional(receipt.ReceiptKind);
        var accent = provisional ? Colors.Orange.Darken3 : Colors.Green.Darken3;
        var name = student != null
            ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
            : applicantName ?? "Not recorded";

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontSize(10).FontFamily(PortablePdfFonts.DefaultFamily, PortablePdfFonts.GujaratiFamily));
            page.Header().BorderBottom(2).BorderColor(accent).PaddingBottom(12).Column(column =>
            {
                column.Item().Text(currentTenant?.Name ?? "ShikshaMitra").FontSize(18).Bold();
                column.Item().PaddingTop(6).Text(provisional ? "PROVISIONAL RECEIPT" : "OFFICIAL RECEIPT")
                    .FontSize(16).Bold().FontColor(accent);
                column.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text($"Receipt number: {receipt.ReceiptNumber}").SemiBold();
                    row.AutoItem().Text($"Issued: {receipt.ReceiptDate:dd-MMM-yyyy}");
                });
            });
            page.Content().PaddingTop(18).Column(column =>
            {
                column.Spacing(12);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(110); c.RelativeColumn(); });
                    void Fact(string label, string value)
                    {
                        table.Cell().PaddingVertical(4).Text(label).FontColor(Colors.Grey.Darken2);
                        table.Cell().PaddingVertical(4).Text(value);
                    }
                    Fact(student == null && applicantName != null ? "Applicant" : "Student", name);
                    Fact("GR number", student?.GrNo?.ToString() ?? "Not applicable");
                    Fact("Academic year", academicYear?.Name ?? "Not recorded");
                    Fact("Receipt covers", FeeReceiptPresentation.GetScopeLabel(receipt.ReceiptScope));
                    Fact("Payment method", payments.Count == 0 ? "Not recorded" : string.Join(", ", payments
                        .Select(p => p.PaymentMethod).Distinct().OrderBy(m => m).Select(FeeReceiptPresentation.GetPaymentMethodLabel)));
                    if (payments.Count > 0)
                        Fact("Payment received", payments.Min(p => p.PaymentDate).ToString("dd-MMM-yyyy"));
                    if (predecessor != null) Fact("Provisional receipt", predecessor.ReceiptNumber);
                });
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(); });
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(8).Text("Description").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(8).Text("Installment").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(8).AlignRight().Text("Amount").SemiBold();
                    });
                    void Line(string description, string period, decimal amount)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(description);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(period);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text(Money(amount));
                    }
                    foreach (var line in feeLines.OrderBy(a => a.DueDate).ThenBy(a => a.FeeHeadName))
                        Line(line.FeeHeadName ?? "Fee payment", line.InstallmentLabel ?? "—", (decimal)line.Amount);
                    var feeTotal = receipt.ReceiptScope == FeeReceiptScope.Combined ? receipt.FeePaidAmount
                        : receipt.ReceiptScope == FeeReceiptScope.Fee ? (decimal)receipt.TotalPaidAmount : 0m;
                    var remainder = feeTotal - feeLines.Sum(a => (decimal)a.Amount);
                    if (remainder > 0) Line(feeLines.Count == 0 ? "Fees (including applicable late fees)" : "Late fees / other fee allocations", "—", remainder);
                    if (receipt.ReceiptScope is FeeReceiptScope.Charge or FeeReceiptScope.Combined)
                        Line("Charges", "—", receipt.ReceiptScope == FeeReceiptScope.Combined ? receipt.ChargePaidAmount : (decimal)receipt.TotalPaidAmount);
                });
                column.Item().Background(Colors.Grey.Lighten3).Padding(12).Row(row =>
                {
                    row.RelativeItem().Text("Total amount received").Bold();
                    row.AutoItem().Text(Money((decimal)receipt.TotalPaidAmount)).FontSize(14).Bold();
                });
                column.Item().Text(provisional
                    ? "Subject to payment verification. An official receipt will be issued after approval in reconciliation. This document does not confirm bank clearance."
                    : predecessor != null
                        ? "Payment verified. This official receipt supersedes the provisional receipt referenced above; both documents represent the same payment."
                        : payments.Count > 0 && payments.All(p => p.PaymentMethod == ShikshaMitra.Finance.Fees.Collections.PaymentMethodEnum.Cash)
                            ? "Cash payment received. This official receipt acknowledges the amount shown above."
                            : "This official receipt acknowledges the payment recorded above.")
                    .FontSize(9).FontColor(accent);
                if (!string.IsNullOrWhiteSpace(receipt.Remarks)) column.Item().Text($"Remarks: {receipt.Remarks}").FontSize(9);
                column.Item().PaddingTop(24).Row(row =>
                {
                    row.RelativeItem().Text("Authorised signatory");
                    row.RelativeItem().AlignRight().Text("School seal");
                });
            });
            page.Footer().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text("Retain this receipt for your records. Currency: INR.").FontSize(8).FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(text => { text.CurrentPageNumber(); text.Span(" / "); text.TotalPages(); });
            });
        })).GeneratePdf();
    }
}
