using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeReceiptTests;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ReceiptLifecycleApi_Tests : ShikshaMitraApiTestBase
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public ReceiptLifecycleApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory)
    {
        FeeCollectionTestHelper.DefaultAcademicYearId = ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId = ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId = ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId = ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
    }

    [Theory]
    [InlineData(PaymentMethodEnum.Cash, false, false)]
    [InlineData(PaymentMethodEnum.Cash, true, false)]
    [InlineData(PaymentMethodEnum.Cheque, false, false)]
    [InlineData(PaymentMethodEnum.DemandDraft, false, false)]
    [InlineData(PaymentMethodEnum.BankTransfer, false, false)]
    [InlineData(PaymentMethodEnum.BankTransfer, true, false)]
    [InlineData(PaymentMethodEnum.CreditCard, false, false)]
    [InlineData(PaymentMethodEnum.DebitCard, false, false)]
    [InlineData(PaymentMethodEnum.UPI, false, false)]
    [InlineData(PaymentMethodEnum.UPI, true, false)]
    [InlineData(PaymentMethodEnum.UPI, true, true)]
    [TestTier(TestTraits.Tiers.Critical)]
    public async Task Collect_List_Download_Approve_Or_Reject_Preserves_Payment_And_History(
        PaymentMethodEnum method, bool includeCharge, bool reject)
    {
        var (studentId, boardId) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
        var feeHeadId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, feeHeadId, 100d);
        var input = FeeCollectionTestHelper.BuildCollectFeeInput(studentId, Factory.SeededAcademicYearId, feeHeadId, dueFeeId, 100d);
        if (method != PaymentMethodEnum.Cash)
        {
            using var scope = Factory.Services.CreateScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
            using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var bank = new BankAccount(Guid.NewGuid(), Factory.SeededAcademicYearId, "Synthetic Test Bank", "Test branch", null,
                "Synthetic School", Enum.GetValues<BankAccountType>().First(),
                BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0).ToString(), "Receipt lifecycle testing", "TEST0001234", "synthetic@test", tenantId: TenantId);
            await scope.ServiceProvider.GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(bank);
            await uow.CompleteAsync();
            input.Payments[0].BankAccountId = bank.Id;
        }
        if (includeCharge)
        {
            using var scope = Factory.Services.CreateScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
            using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var charge = new Charge(Guid.NewGuid(), TenantId, Factory.SeededAcademicYearId, Guid.NewGuid(), "Receipt lifecycle test charge",
                Guid.NewGuid(), studentId, boardId, Factory.SeededMediumId, Factory.SeededStandardId, Factory.SeededDivisionId,
                "Synthetic receipt lifecycle verification", 25m, DateTimeOffset.UtcNow.AddDays(1), (ChargePriority)0,
                ChargeStatus.Pending, Guid.NewGuid());
            var head = await scope.ServiceProvider.GetRequiredService<IRepository<FeeHead, Guid>>().GetAsync(feeHeadId);
            charge.SetAccountingSnapshot(head.IncomeCategoryId);
            charge.Approve(Guid.NewGuid(), "Synthetic approval");
            await scope.ServiceProvider.GetRequiredService<IRepository<Charge, Guid>>().InsertAsync(charge);
            await uow.CompleteAsync();
            input.ChargePayments = [new() { ChargeId = charge.Id, Amount = 25d }];
            input.Payments[0].Amount = 125d;
        }
        input.Payments[0].PaymentMethod = method;
        input.Payments[0].Details = method switch
        {
            PaymentMethodEnum.Cheque => new() { ["chequeNumber"] = "123456", ["bankName"] = "Test bank", ["chequeDate"] = DateTime.UtcNow.ToString("O") },
            PaymentMethodEnum.DemandDraft => new() { ["ddNumber"] = "123456", ["bankName"] = "Test bank", ["ddDate"] = DateTime.UtcNow.ToString("O") },
            PaymentMethodEnum.UPI => new() { ["transactionId"] = "TEST-UPI-" + Guid.NewGuid().ToString("N"), ["upiId"] = "synthetic@test" },
            PaymentMethodEnum.BankTransfer => new() { ["transactionId"] = "N123456789876", ["bankName"] = "Test bank",
                ["transferDate"] = "2026-10-20", ["accountHolderName"] = "Rahul Sharma" },
            PaymentMethodEnum.CreditCard => new() { ["transactionId"] = "TEST-CARD", ["cardLast4"] = "1234", ["bankName"] = "Test bank" },
            _ => null
        };
        // Even a legacy cashier's YES cannot finalize an electronic payment.
        input.PaymentConfirmed = method is PaymentMethodEnum.Cash or PaymentMethodEnum.Cheque or PaymentMethodEnum.DemandDraft ? null : true;
        var collected = await Client.PostAsJsonAsync("/api/fee-collections", input, JsonOptions);
        collected.StatusCode.ShouldBe(HttpStatusCode.Created, await collected.Content.ReadAsStringAsync());
        var output = (await collected.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions))!;
        var cash = method == PaymentMethodEnum.Cash;
        output.Status.ShouldBe(cash ? FeeCollectionStatus.Paid : FeeCollectionStatus.InReconciliation);

        var receiptsUrl = $"/api/v1/fee-receipts?academicYearId={Factory.SeededAcademicYearId}&boardId={boardId}&educationMediumId={Factory.SeededMediumId}&maxResultCount=100";
        var history = (await Client.GetFromJsonAsync<PagedResultDto<FeeReceiptListItemDto>>(receiptsUrl, JsonOptions))!;
        var originals = history.Items.Where(r => r.FeeCollectionTransactionId == output.Id).ToList();
        originals.Count.ShouldBe(includeCharge ? 2 : 1);
        originals.Sum(r => r.TotalPaidAmount).ShouldBe(includeCharge ? 125d : 100d);
        foreach (var document in originals)
        {
            document.ReceiptKindLabel.ShouldStartWith(cash ? "Official Receipt" : "Provisional Receipt");
            document.PaymentMethodLabel.ShouldBe(FeeReceiptPresentation.GetPaymentMethodLabel(method));
            document.VerificationStatus.ShouldBe(cash ? FeeReceiptVerificationStatus.NotApplicable : FeeReceiptVerificationStatus.PendingVerification);
            await CheckPdfAsync(document.Id, document.PdfFileName, !cash && method == PaymentMethodEnum.UPI && !includeCharge,
                method == PaymentMethodEnum.BankTransfer);
        }
        var retry = await Client.PostAsJsonAsync("/api/fee-collections", input, JsonOptions);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await retry.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions))!.Id.ShouldBe(output.Id);

        var inboxUrl = $"/api/v1/finance/reconciliations?academicYearId={Factory.SeededAcademicYearId}&paymentMethod={method}&pageSize=100";
        var inbox = await Client.GetFromJsonAsync<System.Text.Json.JsonElement>(inboxUrl, JsonOptions);
        inbox.GetProperty("items").EnumerateArray().Count(r => r.GetProperty("feeCollectionTransactionId").GetGuid() == output.Id).ShouldBe(originals.Count);
        if (!cash)
        {
            var actionUrl = $"/api/v1/finance/reconciliations/{originals[0].Id}/{(reject ? "reject" : "approve")}";
            var action = await Client.PatchAsJsonAsync(actionUrl, new { remarks = "Synthetic bank verification decision" }, JsonOptions);
            action.StatusCode.ShouldBe(HttpStatusCode.OK, await action.Content.ReadAsStringAsync());
            foreach (var original in originals)
            {
                var detail = (await Client.GetFromJsonAsync<FeeReceiptDetailDto>($"/api/v1/fee-receipts/{original.Id}", JsonOptions))!;
                detail.VerificationStatus.ShouldBe(reject ? FeeReceiptVerificationStatus.Rejected : FeeReceiptVerificationStatus.Verified);
                if (reject) detail.LinkedFinalReceiptId.ShouldBeNull();
                else
                {
                    var official = (await Client.GetFromJsonAsync<FeeReceiptDetailDto>($"/api/v1/fee-receipts/{detail.LinkedFinalReceiptId}", JsonOptions))!;
                    official.LinkedProvisionalReceiptId.ShouldBe(original.Id);
                    official.TotalPaidAmount.ShouldBe(original.TotalPaidAmount);
                    official.PaymentMethodLabel.ShouldBe(original.PaymentMethodLabel);
                    official.ReceiptKindLabel.ShouldStartWith("Official Receipt");
                    await CheckPdfAsync(official.Id, official.PdfFileName, method == PaymentMethodEnum.UPI && !includeCharge,
                        method == PaymentMethodEnum.BankTransfer);
                }
            }
            var duplicate = await Client.PatchAsJsonAsync(actionUrl, new { remarks = "Duplicate synthetic decision" }, JsonOptions);
            duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
        using var verifyScope = Factory.Services.CreateScope();
        using var verifyTenant = verifyScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
        using var verifyUow = verifyScope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var due = await verifyScope.ServiceProvider.GetRequiredService<IDueFeeRepository>().GetAsync(dueFeeId);
        due.PaidAmount.ShouldBe(reject ? 0m : 100m);
        var documents = await verifyScope.ServiceProvider.GetRequiredService<IFeePaymentReceiptRepository>().GetListAsync(r => r.FeeCollectionTransactionId == output.Id);
        documents.Count.ShouldBe(originals.Count * (!cash && !reject ? 2 : 1));
        await verifyUow.CompleteAsync();
    }

    private async Task CheckPdfAsync(Guid receiptId, string filename, bool saveExample, bool bankTransfer = false)
    {
        var response = await Client.GetAsync($"/api/v1/fee-receipts/{receiptId}/pdf");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var disposition = response.Content.Headers.ContentDisposition!;
        (disposition.FileNameStar ?? disposition.FileName?.Trim('"')).ShouldBe(filename);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        if (bankTransfer)
        {
            var text = Accounts.Reconciliation.ReceiptPdfTextReader.Read(bytes);
            text.ShouldContain("N123456789876");
            text.ShouldContain("20-Oct-2026");
            text.ShouldContain("Rahul Sharma");
            text.ShouldContain("Synthetic Test Bank");
        }
        var directory = Environment.GetEnvironmentVariable("RECEIPT_E2E_PDF_DIRECTORY");
        if (saveExample && !string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, filename), bytes);
        }
    }
}
