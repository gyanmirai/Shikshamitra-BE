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
