using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Application.Finance.Fees.Receipts;

/// <summary>
/// Generates PDF receipts using QuestPDF.
/// Selects the appropriate layout based on <see cref="FeeReceiptKind"/>.
/// </summary>
public class FeeReceiptPdfGenerator : IFeeReceiptPdfGenerator, ITransientDependency
{
    private readonly IReadOnlyRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository;
    private readonly IReadOnlyRepository<FeeCollectionDueFeeAllocation, Guid> feeCollectionDueFeeAllocationRepository;
    private readonly IReadOnlyRepository<DueFee, Guid> dueFeeRepository;
    private readonly IReadOnlyRepository<FeeHead, Guid> feeHeadRepository;
    private readonly IReadOnlyRepository<Standard, Guid> standardRepository;
    private readonly IReadOnlyRepository<Division, Guid> divisionRepository;
    private readonly IReadOnlyRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository;
    private readonly IReadOnlyRepository<Charge, Guid> chargeRepository;
    private readonly IReadOnlyRepository<ChargeCategory, Guid> chargeCategoryRepository;

    public FeeReceiptPdfGenerator(
        IReadOnlyRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
        IReadOnlyRepository<FeeCollectionDueFeeAllocation, Guid> feeCollectionDueFeeAllocationRepository,
        IReadOnlyRepository<DueFee, Guid> dueFeeRepository,
        IReadOnlyRepository<FeeHead, Guid> feeHeadRepository,
        IReadOnlyRepository<Standard, Guid> standardRepository,
        IReadOnlyRepository<Division, Guid> divisionRepository,
        IReadOnlyRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
        IReadOnlyRepository<Charge, Guid> chargeRepository,
        IReadOnlyRepository<ChargeCategory, Guid> chargeCategoryRepository)
    {
        this.paymentMethodRecordRepository = paymentMethodRecordRepository;
        this.feeCollectionDueFeeAllocationRepository = feeCollectionDueFeeAllocationRepository;
        this.dueFeeRepository = dueFeeRepository;
        this.feeHeadRepository = feeHeadRepository;
        this.standardRepository = standardRepository;
        this.divisionRepository = divisionRepository;
        this.chargeCollectionRecordRepository = chargeCollectionRecordRepository;
        this.chargeRepository = chargeRepository;
        this.chargeCategoryRepository = chargeCategoryRepository;
    }

    public async Task<byte[]> GenerateAsync(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var bytes = receipt.ReceiptKind switch
        {
            FeeReceiptKind.FinalFee => await GenerateFinalFeeReceipt(receipt, student, academicYear, cancellationToken),
            FeeReceiptKind.ProvisionalFee => await GenerateProvisionalFeeReceipt(receipt, student, academicYear, cancellationToken),
            FeeReceiptKind.ProvisionalCharge => await GenerateProvisionalChargeReceipt(receipt, student, academicYear, cancellationToken),
            _ => await GenerateFinalFeeReceipt(receipt, student, academicYear, cancellationToken),
        };

        return bytes;
    }

    // FinalFee template

    private async Task<byte[]> GenerateFinalFeeReceipt(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken)
    {
        var template = LoadFinalFeeReceiptTemplate();
        var model = await BuildFinalTemplateModelAsync(receipt, student, academicYear, cancellationToken);
        var renderedTemplate = RenderFinalFeeReceiptTemplate(template, model);

        if (string.IsNullOrWhiteSpace(renderedTemplate))
            throw new InvalidOperationException("Final fee receipt template rendering returned empty content.");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Portrait());
                page.Margin(20, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(c => BuildHeader(c, "PAYMENT RECEIPT", receipt, Colors.Green.Medium));
                page.Content().Element(c => BuildFinalTemplateDrivenContent(c, model));
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    // ProvisionalFee template

    private async Task<byte[]> GenerateProvisionalFeeReceipt(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken)
    {
        var template = LoadProvisionalFeeReceiptTemplate();
        var model = await BuildFinalTemplateModelAsync(receipt, student, academicYear, cancellationToken);
        var renderedTemplate = RenderFinalFeeReceiptTemplate(template, model);

        if (string.IsNullOrWhiteSpace(renderedTemplate))
            throw new InvalidOperationException("Provisional fee receipt template rendering returned empty content.");

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
                    col.Item().Element(c => BuildFinalTemplateDrivenContent(c, model));
                    col.Item().PaddingTop(5).Text(
                        "* This is a provisional receipt subject to realisation of the instrument. " +
                        "A final receipt will be issued upon bank clearance.")
                        .FontColor(Colors.Red.Medium).FontSize(8).Italic();
                });
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    // ProvisionalCharge template

    private async Task<byte[]> GenerateProvisionalChargeReceipt(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken)
    {
        var template = LoadChargeFeeReceiptTemplate();
        var model = await BuildChargeTemplateModelAsync(receipt, student, academicYear, cancellationToken);
        var renderedTemplate = RenderChargeFeeReceiptTemplate(template, model);

        if (string.IsNullOrWhiteSpace(renderedTemplate))
            throw new InvalidOperationException("Charge receipt template rendering returned empty content.");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5.Landscape());
                page.Margin(20, Unit.Point);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Element(c => BuildHeader(c, "CHARGE RECEIPT (KACHI)", receipt, Colors.Blue.Medium));
                page.Content().Element(c => BuildChargeTemplateDrivenContent(c, model));
                page.Footer().Element(BuildFooter);
            });
        }).GeneratePdf();
    }

    // Shared content builders

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
            : "-";

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
                    "GR Number", student?.GrNo?.ToString() ?? "-");
                AddRow("Academic Year", academicYear?.Name ?? "-",
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

    private static void BuildFinalTemplateDrivenContent(IContainer container, FinalTemplateModel model)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                });

                table.Cell().Border(1).Padding(8).Column(left =>
                {
                    left.Item().Text("Student").Bold().FontSize(11);
                    left.Item().PaddingTop(4).Text($"Name: {model.StudentName}");
                    left.Item().Text($"GR: {model.StudentGRNumber}");
                    left.Item().Text($"Class: {model.StandardName} - {model.DivisionName}");
                    left.Item().Text($"Roll No: {model.StudentRollNumber}");
                    left.Item().Text($"Category: {model.StudentCategory}");
                    left.Item().Text($"Parent: {model.StudentParentName}");
                    left.Item().Text($"Contact: {model.StudentContactNumber}");
                });

                table.Cell().Border(1).Padding(8).Column(right =>
                {
                    right.Item().Text("Payment").Bold().FontSize(11);
                    right.Item().PaddingTop(4).Text($"Mode: {model.PaymentType}");
                    right.Item().Text($"Reference: {model.PaymentReference}");
                    right.Item().Text($"Academic Year: {model.AcademicYear}");
                });
            });

            col.Item().PaddingTop(10).Text($"Fee Details - AY {model.AcademicYear}").Bold();

            foreach (var head in model.FeeHeads)
            {
                col.Item().PaddingTop(6).Text(head.HeadName).SemiBold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Month").SemiBold();
                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Paid").SemiBold();
                    });

                    foreach (var month in head.Months)
                    {
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(month.MonthName);
                        table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                            .AlignRight().Text($"\u20B9 {month.PaidAmount:N2}");
                    }
                });
            }

            col.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(8).AlignRight().Text($"Total Paid: \u20B9 {model.TotalAmount:N2}").Bold();
            col.Item().PaddingTop(4).Text($"In words: {model.TotalAmountInWords}").Italic().FontColor(Colors.Grey.Darken1);
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

    private static string LoadFinalFeeReceiptTemplate()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDirectory, "Templates", "Receipts", "fee-receipt-template.html"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Templates", "Receipts", "fee-receipt-template.html"))
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }
        }

        throw new FileNotFoundException(
            "Final fee receipt template not found at Templates/Receipts/fee-receipt-template.html. " +
            $"Base directory: {baseDirectory}");
    }

    private async Task<FinalTemplateModel> BuildFinalTemplateModelAsync(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken)
    {
        var studentName = student != null
            ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
            : "-";

        var standardName = student?.Standard?.Name ?? "-";

		var divisionName = student?.Division?.Name ?? "-";

		var parentName = student?.FamilyDetail?.FatherDetails?.FullName
            ?? student?.FamilyDetail?.MotherDetails?.FullName
            ?? student?.FamilyDetail?.GuardianDetails?.FullName
            ?? "-";

        var parentContact = student?.FamilyDetail?.FatherDetails?.ContactNo
            ?? student?.FamilyDetail?.MotherDetails?.ContactNo
            ?? student?.FamilyDetail?.GuardianDetails?.ContactNo
            ?? student?.ContactDetail?.PrimaryContactNo
            ?? "-";

        var paymentRecord = (await paymentMethodRecordRepository.GetListAsync(
            p => p.FeeRecordMetadataId == receipt.FeeCollectionTransactionId,
            cancellationToken: cancellationToken)).OrderBy(p => p.CreationTime).FirstOrDefault();

        var paymentType = paymentRecord != null
            ? ToReadablePaymentMethod(paymentRecord.PaymentMethod)
            : "Cash";
        var paymentReference = BuildPaymentReference(paymentRecord);

        var allocations = await feeCollectionDueFeeAllocationRepository.GetListAsync(
            a => a.FeeRecordMetadataId == receipt.FeeCollectionTransactionId,
            cancellationToken: cancellationToken);

        var dueFeeIds = allocations.Select(a => a.DueFeeId).Distinct().ToList();
        var dueFees = dueFeeIds.Count == 0
            ? new List<DueFee>()
            : await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id), cancellationToken: cancellationToken);
        var dueFeeMap = dueFees.ToDictionary(df => df.Id);

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count == 0
            ? new List<FeeHead>()
            : await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id), cancellationToken: cancellationToken);
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var groupedHeads = allocations
            .Where(a => dueFeeMap.ContainsKey(a.DueFeeId))
            .GroupBy(a => dueFeeMap[a.DueFeeId].FeeHeadId)
            .OrderBy(g => feeHeadMap.GetValueOrDefault(g.Key, "Fee Head"))
            .Select(g => new FinalTemplateFeeHeadModel(
                feeHeadMap.GetValueOrDefault(g.Key, "Fee Head"),
                g.Select(a =>
                {
                    var dueFee = dueFeeMap[a.DueFeeId];
                    var statusClass = a.Amount <= 0
                        ? "paid-zero"
                        : (dueFee.OutstandingAmount > 0 ? "paid-partial" : string.Empty);

                    return new FinalTemplateMonthModel(
                        dueFee.Month.ToString(),
                        a.Amount,
                        statusClass,
                        (int)dueFee.Month,
                        dueFee.Year);
                })
                .OrderBy(m => m.YearOrder)
                .ThenBy(m => m.MonthOrder)
                .ToList()))
            .ToList();

        var totalAmount = receipt.TotalPaidAmount;

        return new FinalTemplateModel(
            SchoolName: "ShikshaMitra School",
            SchoolDetails: "Fee Management System",
            SchoolAddress: "-",
            SchoolContactNumber: "-",
            SchoolEmail: "-",
            ReceiptNumber: receipt.ReceiptNumber,
            PaymentType: paymentType,
            AcademicYear: academicYear?.Name ?? "-",
            StudentName: studentName,
            StudentGRNumber: student?.GrNo?.ToString() ?? "-",
            StandardName: standardName,
            DivisionName: divisionName,
            StudentRollNumber: student?.RollNo?.ToString() ?? "-",
            StudentCategory: student?.AdditionalDetail?.Category.ToString() ?? "General",
            StudentParentName: parentName,
            StudentContactNumber: parentContact,
            PaymentReference: paymentReference,
            FeeHeads: groupedHeads,
            TotalAmount: totalAmount,
            TotalAmountInWords: FormatIndianCurrencyWords(totalAmount));
    }

    private static string RenderFinalFeeReceiptTemplate(string template, FinalTemplateModel model)
    {
        var rendered = template;

        rendered = ReplaceEachBlock(rendered, "FeeHeads", BuildFeeHeadsHtml(model.FeeHeads));
        rendered = rendered.Replace("<div id=\"pay-block\"></div>", BuildPaymentBlockHtml(model), StringComparison.Ordinal);

        var replacements = new Dictionary<string, string>
        {
            ["SchoolName"] = model.SchoolName,
            ["SchoolDetails"] = model.SchoolDetails,
            ["SchoolAddress"] = model.SchoolAddress,
            ["SchoolContactNumber"] = model.SchoolContactNumber,
            ["SchoolEmail"] = model.SchoolEmail,
            ["ReceiptNumber"] = model.ReceiptNumber,
            ["PaymentType"] = model.PaymentType,
            ["AcademicYear"] = model.AcademicYear,
            ["StudentName"] = model.StudentName,
            ["StudentGRNumber"] = model.StudentGRNumber,
            ["StandardName"] = model.StandardName,
            ["DivisionName"] = model.DivisionName,
            ["StudentRollNumber"] = model.StudentRollNumber,
            ["StudentCategory"] = model.StudentCategory,
            ["StudentParentName"] = model.StudentParentName,
            ["StudentContactNumber"] = model.StudentContactNumber,
            ["TotalAmount"] = model.TotalAmount.ToString("N2"),
            ["TotalAmountInWords"] = model.TotalAmountInWords,
        };

        foreach (var replacement in replacements)
        {
            rendered = rendered.Replace($"/{{{{{replacement.Key}}}}}", replacement.Value, StringComparison.Ordinal);
            rendered = rendered.Replace($"{{{{{replacement.Key}}}}}", replacement.Value, StringComparison.Ordinal);
        }

        // Replace any remaining unresolved placeholders with default values
        rendered = System.Text.RegularExpressions.Regex.Replace(
            rendered,
            @"\{\{[#/]?[^}]+\}\}",
            m => "N/A");

        return rendered;
    }

    private static string ReplaceEachBlock(string template, string eachName, string replacement)
    {
        var openTag = $"{{{{#each {eachName}}}}}";
        var start = template.IndexOf(openTag, StringComparison.Ordinal);
        if (start < 0)
            return template;

        var index = start;
        var depth = 0;
        while (index < template.Length)
        {
            var nextOpen = template.IndexOf("{{#each", index, StringComparison.Ordinal);
            var nextClose = template.IndexOf("{{/each}}", index, StringComparison.Ordinal);

            if (nextClose < 0)
                break;

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                index = nextOpen + 7;
                continue;
            }

            depth--;
            var closeEnd = nextClose + "{{/each}}".Length;
            index = closeEnd;

            if (depth == 0)
                return template.Substring(0, start) + replacement + template[closeEnd..];
        }

        return template;
    }

    private static string BuildFeeHeadsHtml(IReadOnlyList<FinalTemplateFeeHeadModel> feeHeads)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < feeHeads.Count; i++)
        {
            var head = feeHeads[i];
            sb.Append("<div class=\"fee-group-hdr ");
            if (i > 0)
                sb.Append("sep-hdr");
            sb.Append("\"><span class=\"group-name\">").Append(head.HeadName).Append("</span></div>");

            sb.Append("<div class=\"col-hdr\"><div class=\"col-hdr-lbl\">Month</div><div class=\"col-hdr-lbl\">Paid</div></div>");

            foreach (var month in head.Months)
            {
                sb.Append("<div class=\"month-row\">");
                sb.Append("<div class=\"month-name\">").Append(month.MonthName).Append("</div>");
                sb.Append("<div class=\"month-paid");
                if (!string.IsNullOrWhiteSpace(month.StatusClass))
                    sb.Append(" ").Append(month.StatusClass);
                sb.Append("\">\u20B9").Append(month.PaidAmount.ToString("N2", CultureInfo.InvariantCulture)).Append("</div>");
                sb.Append("</div>");
            }
        }

        return sb.ToString();
    }

    private static string BuildPaymentBlockHtml(FinalTemplateModel model)
    {
        return $"<div class=\"pay-mode\"><div><div class=\"pay-mode-name\">{model.PaymentType}</div><div class=\"pay-mode-sub\">{model.PaymentReference}</div></div></div>";
    }

    private static string BuildPaymentReference(PaymentMethodRecord? paymentRecord)
    {
        if (paymentRecord == null)
            return "CASH";

        return paymentRecord.PaymentDetails switch
        {
            ChequeDetails cheque => $"{cheque.BankName} / {cheque.ChequeNumber}",
            DemandDraftDetails dd => $"{dd.IssuingBankName} / {dd.DDNumber}",
            BankTransferDetails bank => $"{bank.BankName} / {bank.TransactionReferenceNumber}",
            UpiDetails upi => $"{upi.UpiId} / {upi.UpiTransactionId}",
            CreditCardDetails card => $"{card.BankName} / {card.CardLast4}",
            OnlineGatewayDetails gateway => $"{gateway.GatewayName} / {gateway.TransactionId}",
            WalletDetails wallet => $"{wallet.WalletProvider} / {wallet.TransactionId}",
            _ => paymentRecord.PaymentMethod.ToString().ToUpperInvariant(),
        };
    }

    private static string ToReadablePaymentMethod(PaymentMethodEnum paymentMethod)
    {
        return paymentMethod switch
        {
            PaymentMethodEnum.Cash => "Cash",
            PaymentMethodEnum.DemandDraft => "DD",
            PaymentMethodEnum.Cheque => "Cheque",
            PaymentMethodEnum.BankTransfer => "Bank Transfer",
            PaymentMethodEnum.UPI => "UPI",
            PaymentMethodEnum.CreditCard => "Credit Card",
            PaymentMethodEnum.DebitCard => "Debit Card",
            _ => paymentMethod.ToString(),
        };
    }

    private static string FormatIndianCurrencyWords(double amount)
    {
        var absolute = Math.Abs(amount);
        var rupees = (long)Math.Floor(absolute);
        var paise = (int)Math.Round((absolute - rupees) * 100, MidpointRounding.AwayFromZero);
        if (paise == 100)
        {
            rupees += 1;
            paise = 0;
        }

        var words = $"{NumberToWords(rupees)} rupees";
        if (paise > 0)
            words += $" and {NumberToWords(paise)} paise";

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words) + " only";
    }

    private static string NumberToWords(long number)
    {
        if (number == 0)
            return "zero";

        string[] units =
        [
            "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
            "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"
        ];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

        static string TwoDigits(long value, string[] localUnits, string[] localTens)
        {
            if (value < 20)
                return localUnits[(int)value];

            return localTens[(int)(value / 10)] + (value % 10 > 0 ? " " + localUnits[(int)(value % 10)] : "");
        }

        static string ThreeDigits(long value, string[] localUnits, string[] localTens)
        {
            var hundred = value / 100;
            var rest = value % 100;
            var result = hundred > 0 ? localUnits[(int)hundred] + " hundred" : "";
            if (rest > 0)
                result += (result.Length > 0 ? " " : "") + TwoDigits(rest, localUnits, localTens);

            return result;
        }

        var parts = new List<string>();
        var crore = number / 10000000;
        number %= 10000000;
        var lakh = number / 100000;
        number %= 100000;
        var thousand = number / 1000;
        number %= 1000;

        if (crore > 0)
            parts.Add($"{NumberToWords(crore)} crore");

        if (lakh > 0)
            parts.Add($"{NumberToWords(lakh)} lakh");

        if (thousand > 0)
            parts.Add($"{NumberToWords(thousand)} thousand");

        if (number > 0)
            parts.Add(ThreeDigits(number, units, tens));

        return string.Join(" ", parts);
    }

    private sealed record FinalTemplateModel(
        string SchoolName,
        string SchoolDetails,
        string SchoolAddress,
        string SchoolContactNumber,
        string SchoolEmail,
        string ReceiptNumber,
        string PaymentType,
        string AcademicYear,
        string StudentName,
        string StudentGRNumber,
        string StandardName,
        string DivisionName,
        string StudentRollNumber,
        string StudentCategory,
        string StudentParentName,
        string StudentContactNumber,
        string PaymentReference,
        IReadOnlyList<FinalTemplateFeeHeadModel> FeeHeads,
        double TotalAmount,
        string TotalAmountInWords);

    private sealed record FinalTemplateFeeHeadModel(
        string HeadName,
        IReadOnlyList<FinalTemplateMonthModel> Months);

    private sealed record FinalTemplateMonthModel(
        string MonthName,
        double PaidAmount,
        string StatusClass,
        int MonthOrder,
        int YearOrder);

    private static string LoadProvisionalFeeReceiptTemplate()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDirectory, "Templates", "Receipts", "provisional-fee-receipt-template.html"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Templates", "Receipts", "provisional-fee-receipt-template.html"))
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }
        }

        throw new FileNotFoundException(
            "Provisional fee receipt template not found at Templates/Receipts/provisional-fee-receipt-template.html. " +
            $"Base directory: {baseDirectory}");
    }

    private static string LoadChargeFeeReceiptTemplate()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidatePaths = new[]
        {
            Path.Combine(baseDirectory, "Templates", "Receipts", "charge-fee-receipt-template.html"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Templates", "Receipts", "charge-fee-receipt-template.html"))
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content;
                }
            }
        }

        throw new FileNotFoundException(
            "Charge fee receipt template not found at Templates/Receipts/charge-fee-receipt-template.html. " +
            $"Base directory: {baseDirectory}");
    }

    private async Task<ChargeTemplateModel> BuildChargeTemplateModelAsync(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken)
    {
        var studentName = student != null
            ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
            : "-";

        var standardName = student?.Standard?.Name ?? "-";
        var divisionName = student?.Division?.Name ?? "-";

        var parentName = student?.FamilyDetail?.FatherDetails?.FullName
            ?? student?.FamilyDetail?.MotherDetails?.FullName
            ?? student?.FamilyDetail?.GuardianDetails?.FullName
            ?? "-";

        var parentContact = student?.FamilyDetail?.FatherDetails?.ContactNo
            ?? student?.FamilyDetail?.MotherDetails?.ContactNo
            ?? student?.FamilyDetail?.GuardianDetails?.ContactNo
            ?? student?.ContactDetail?.PrimaryContactNo
            ?? "-";

        var paymentRecord = (await paymentMethodRecordRepository.GetListAsync(
            p => p.FeeRecordMetadataId == receipt.FeeCollectionTransactionId,
            cancellationToken: cancellationToken)).OrderBy(p => p.CreationTime).FirstOrDefault();

        var paymentType = paymentRecord != null
            ? ToReadablePaymentMethod(paymentRecord.PaymentMethod)
            : "Cash";
        var paymentReference = BuildPaymentReference(paymentRecord);

        var chargeRecords = await chargeCollectionRecordRepository.GetListAsync(
            c => c.FeeRecordMetadataId == receipt.FeeCollectionTransactionId,
            cancellationToken: cancellationToken);

        var chargeIds = chargeRecords.Select(cr => cr.ChargeId).Distinct().ToList();
        var charges = chargeIds.Count == 0
            ? new List<Charge>()
            : await chargeRepository.GetListAsync(c => chargeIds.Contains(c.Id), cancellationToken: cancellationToken);
        var chargeMap = charges.ToDictionary(c => c.Id);

        var categoryIds = charges.Select(c => c.ChargeCategoryId).Distinct().ToList();
        var categories = categoryIds.Count == 0
            ? new List<ChargeCategory>()
            : await chargeCategoryRepository.GetListAsync(cc => categoryIds.Contains(cc.Id), cancellationToken: cancellationToken);
        var categoryMap = categories.ToDictionary(cc => cc.Id);

        var chargeLines = new List<ChargeTemplateLineModel>();
        foreach (var cr in chargeRecords)
        {
            if (chargeMap.TryGetValue(cr.ChargeId, out var charge))
            {
                categoryMap.TryGetValue(charge.ChargeCategoryId, out var cat);
                var categoryName = cat?.Name ?? "Miscellaneous";

                var typeClass = "tp-misc";
                if (categoryName.Contains("Fine", StringComparison.OrdinalIgnoreCase))
                    typeClass = "tp-fine";
                else if (categoryName.Contains("Deposit", StringComparison.OrdinalIgnoreCase))
                    typeClass = "tp-dep";

                chargeLines.Add(new ChargeTemplateLineModel(
                    typeClass,
                    categoryName,
                    charge.ChargeName,
                    "Paid",
                    cr.Amount));
            }
            else
            {
                chargeLines.Add(new ChargeTemplateLineModel(
                    "tp-misc",
                    "Charge",
                    "Applied Charge",
                    "Paid",
                    cr.Amount));
            }
        }

        var totalAmount = receipt.TotalPaidAmount;

        var receiptDateStr = receipt.ReceiptDate.ToString("dd MMM yyyy");

        return new ChargeTemplateModel(
            SchoolName: "ShikshaMitra School",
            SchoolDetails: "Fee Management System",
            SchoolAddress: "-",
            SchoolContactNumber: "-",
            SchoolEmail: "-",
            ReceiptNumber: receipt.ReceiptNumber,
            PaymentType: paymentType,
            AcademicYear: academicYear?.Name ?? "-",
            StudentName: studentName,
            StudentGRNumber: student?.GrNo?.ToString() ?? "-",
            StandardName: standardName,
            DivisionName: divisionName,
            StudentRollNumber: student?.RollNo?.ToString() ?? "-",
            StudentCategory: student?.AdditionalDetail?.Category.ToString() ?? "General",
            StudentParentName: parentName,
            StudentContactNumber: parentContact,
            PaymentReference: paymentReference,
            ReceiptDate: receiptDateStr,
            ReceiptTime: "receiptTimeStr",
            AmountTendered: totalAmount.ToString("N2"),
            ChangeReturned: "0.00",
            CounterName: "Fee Counter — 01",
            CollectedBy: "Accountant",
            Charges: chargeLines,
            TotalAmount: totalAmount,
            TotalAmountInWords: FormatIndianCurrencyWords(totalAmount),
            AccountantName: "Authorised Signatory");
    }

    private static void BuildChargeTemplateDrivenContent(IContainer container, ChargeTemplateModel model)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1);
                    cols.RelativeColumn(1);
                });

                table.Cell().Border(1).Padding(8).Column(left =>
                {
                    left.Item().Text("Student").Bold().FontSize(11);
                    left.Item().PaddingTop(4).Text($"Name: {model.StudentName}");
                    left.Item().Text($"GR: {model.StudentGRNumber}");
                    left.Item().Text($"Class: {model.StandardName} - {model.DivisionName}");
                    left.Item().Text($"Roll No: {model.StudentRollNumber}");
                    left.Item().Text($"Category: {model.StudentCategory}");
                    left.Item().Text($"Parent: {model.StudentParentName}");
                    left.Item().Text($"Contact: {model.StudentContactNumber}");
                });

                table.Cell().Border(1).Padding(8).Column(right =>
                {
                    right.Item().Text("Payment").Bold().FontSize(11);
                    right.Item().PaddingTop(4).Text($"Mode: {model.PaymentType}");
                    right.Item().Text($"Reference: {model.PaymentReference}");
                    right.Item().Text($"Academic Year: {model.AcademicYear}");
                    right.Item().Text($"Date: {model.ReceiptDate} {model.ReceiptTime}");
                });
            });

            col.Item().PaddingTop(10).Text($"Charges Details - AY {model.AcademicYear}").Bold();

            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(1); // Type
                    cols.RelativeColumn(2); // Description
                    cols.RelativeColumn(1); // Status
                    cols.RelativeColumn(1); // Amount
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Type").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Description").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Status").SemiBold();
                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Amount").SemiBold();
                });

                foreach (var charge in model.Charges)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(charge.ChargeTypeName);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(charge.Description);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(charge.Status);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .AlignRight().Text($"\u20B9 {charge.Amount:N2}");
                }
            });

            col.Item().PaddingTop(10).Background(Colors.Grey.Lighten3).Padding(8).AlignRight().Text($"Total Paid: \u20B9 {model.TotalAmount:N2}").Bold();
            col.Item().PaddingTop(4).Text($"In words: {model.TotalAmountInWords}").Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    private static string RenderChargeFeeReceiptTemplate(string template, ChargeTemplateModel model)
    {
        var rendered = template;

        rendered = ReplaceEachBlock(rendered, "Charges", BuildChargesHtml(model.Charges));

        var replacements = new Dictionary<string, string>
        {
            ["SchoolName"] = model.SchoolName,
            ["SchoolDetails"] = model.SchoolDetails,
            ["SchoolAddress"] = model.SchoolAddress,
            ["SchoolContactNumber"] = model.SchoolContactNumber,
            ["SchoolEmail"] = model.SchoolEmail,
            ["ReceiptNumber"] = model.ReceiptNumber,
            ["PaymentType"] = model.PaymentType,
            ["AcademicYear"] = model.AcademicYear,
            ["StudentName"] = model.StudentName,
            ["StudentGRNumber"] = model.StudentGRNumber,
            ["StandardName"] = model.StandardName,
            ["DivisionName"] = model.DivisionName,
            ["StudentRollNumber"] = model.StudentRollNumber,
            ["StudentCategory"] = model.StudentCategory,
            ["StudentParentName"] = model.StudentParentName,
            ["StudentContactNumber"] = model.StudentContactNumber,
            ["PaymentReference"] = model.PaymentReference,
            ["ReceiptDate"] = model.ReceiptDate,
            ["ReceiptTime"] = model.ReceiptTime,
            ["AmountTendered"] = model.AmountTendered,
            ["ChangeReturned"] = model.ChangeReturned,
            ["CounterName"] = model.CounterName,
            ["CollectedBy"] = model.CollectedBy,
            ["TotalAmount"] = model.TotalAmount.ToString("N2"),
            ["TotalAmountInWords"] = model.TotalAmountInWords,
            ["AccountantName"] = model.AccountantName
        };

        foreach (var replacement in replacements)
        {
            rendered = rendered.Replace($"/{{{{{replacement.Key}}}}}", replacement.Value, StringComparison.Ordinal);
            rendered = rendered.Replace($"{{{{{replacement.Key}}}}}", replacement.Value, StringComparison.Ordinal);
        }

        // Replace any remaining unresolved placeholders with default values
        rendered = System.Text.RegularExpressions.Regex.Replace(
            rendered,
            @"\{\{[#/]?[^}]+\}\}",
            m => "N/A");

        return rendered;
    }

    private static string BuildChargesHtml(IReadOnlyList<ChargeTemplateLineModel> charges)
    {
        var sb = new StringBuilder();

        foreach (var charge in charges)
        {
            sb.Append("<tr>");
            sb.Append("<td><span class=\"typ-pill ").Append(charge.ChargeTypeClass).Append("\">").Append(charge.ChargeTypeName).Append("</span></td>");
            sb.Append("<td>").Append(charge.Description).Append("</td>");
            sb.Append("<td><span class=\"st-paid\">").Append(charge.Status).Append("</span></td>");
            sb.Append("<td>\u20B9").Append(charge.Amount.ToString("N2", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("</tr>");
        }

        return sb.ToString();
    }

    private sealed record ChargeTemplateModel(
        string SchoolName,
        string SchoolDetails,
        string SchoolAddress,
        string SchoolContactNumber,
        string SchoolEmail,
        string ReceiptNumber,
        string PaymentType,
        string AcademicYear,
        string StudentName,
        string StudentGRNumber,
        string StandardName,
        string DivisionName,
        string StudentRollNumber,
        string StudentCategory,
        string StudentParentName,
        string StudentContactNumber,
        string PaymentReference,
        string ReceiptDate,
        string ReceiptTime,
        string AmountTendered,
        string ChangeReturned,
        string CounterName,
        string CollectedBy,
        IReadOnlyList<ChargeTemplateLineModel> Charges,
        double TotalAmount,
        string TotalAmountInWords,
        string AccountantName);

    private sealed record ChargeTemplateLineModel(
        string ChargeTypeClass,
        string ChargeTypeName,
        string Description,
        string Status,
        double Amount);
}
