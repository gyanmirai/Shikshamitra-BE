using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Application.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class FeeReceiptPdfGenerator_Tests
{
    [Theory]
    [InlineData(FeeReceiptScope.Fee, 20000)]
    [InlineData(FeeReceiptScope.Charge, 60600)]
    [InlineData(FeeReceiptScope.Combined, 80600)]
    public async Task Renders_Principal_And_Late_Fees_On_Their_Selected_Receipt(FeeReceiptScope scope, int total)
    {
        var transactionId = Guid.NewGuid();
        var line = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), null, transactionId, Guid.NewGuid(), 20000);
        line.SetScheduleSnapshot("School fees", "June 2026", null, null,
            new DateTime(2026, 6, 5), ShikshaMitra.Finance.Fees.Settings.PaymentTerms.ScheduleSource.Legacy);
        var allocations = Substitute.For<IReadOnlyRepository<FeeCollectionDueFeeAllocation, Guid>>();
        allocations.GetListAsync(Arg.Any<Expression<Func<FeeCollectionDueFeeAllocation, bool>>>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(new List<FeeCollectionDueFeeAllocation> { line });
        var receipt = new FeePaymentReceipt(Guid.NewGuid())
        {
            StudentId = Guid.NewGuid(), FeeCollectionTransactionId = transactionId,
            ReceiptNumber = $"SPLIT-{scope}-2026", ReceiptScope = scope,
            ReceiptKind = scope == FeeReceiptScope.Charge ? FeeReceiptKind.FinalCharge : FeeReceiptKind.FinalFee,
            TotalPaidAmount = total, ReceiptDate = new DateOnly(2026, 10, 30),
            Remarks = "Synthetic receipt grouping verification."
        };
        if (scope == FeeReceiptScope.Combined) receipt.SetAmountBreakdown(20000m, 60600m);
        receipt.SetLateFeePaidAmount(scope == FeeReceiptScope.Fee ? 0m : 60600m);

        var bytes = await new FeeReceiptPdfGenerator(allocations: allocations).GenerateAsync(receipt, null, null);

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var output = Environment.GetEnvironmentVariable("RECEIPT_PDF_QA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Directory.CreateDirectory(output);
            await File.WriteAllBytesAsync(Path.Combine(output, $"receipt-split-{scope}.pdf"), bytes);
        }
    }

    [Theory]
    [InlineData(FeeReceiptKind.FinalFee, FeeReceiptScope.Fee, PaymentMethodEnum.Cash)]
    [InlineData(FeeReceiptKind.ProvisionalFee, FeeReceiptScope.Combined, PaymentMethodEnum.CreditCard)]
    [InlineData(FeeReceiptKind.FinalCharge, FeeReceiptScope.Charge, PaymentMethodEnum.CreditCard)]
    public async Task Renders_Official_Provisional_And_Charge_Evidence(FeeReceiptKind kind, FeeReceiptScope scope, PaymentMethodEnum method)
    {
        var transactionId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Name.Returns("Demonstration School");
        var payments = Substitute.For<IReadOnlyRepository<PaymentMethodRecord, Guid>>();
        payments.GetListAsync(Arg.Any<Expression<Func<PaymentMethodRecord, bool>>>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new List<PaymentMethodRecord>
            {
                PaymentMethodRecord.Create(studentId, transactionId, 12345.67m,
                    new DateTimeOffset(2026, 10, 23, 10, 30, 0, TimeSpan.FromHours(5.5)), method)
            }));
        var receipt = new FeePaymentReceipt(Guid.NewGuid())
        {
            StudentId = studentId, FeeCollectionTransactionId = transactionId,
            ReceiptNumber = "TEST-2026-000001", ReceiptKind = kind, ReceiptScope = scope,
            TotalPaidAmount = 12345.67, ReceiptDate = new DateOnly(2026, 10, 23),
            Remarks = "Sample receipt for PDF layout verification."
        };
        if (scope == FeeReceiptScope.Combined) receipt.SetAmountBreakdown(10000m, 2345.67m);
        var student = new Student(studentId, null, "Alexandra", "Prerna", "Long Student Surname for Layout Verification",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), grNo: 10213);
        var generator = new FeeReceiptPdfGenerator(paymentRecords: payments, currentTenant: tenant);

        var bytes = await generator.GenerateAsync(receipt, student, null);

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        await payments.Received(1).GetListAsync(Arg.Any<Expression<Func<PaymentMethodRecord, bool>>>(),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
        var outputDirectory = Environment.GetEnvironmentVariable("RECEIPT_PDF_QA_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllBytesAsync(Path.Combine(outputDirectory,
                FeeReceiptPresentation.GetPdfFileName(kind, scope, receipt.ReceiptNumber, receipt.ReceiptDate)), bytes);
        }
    }
}
