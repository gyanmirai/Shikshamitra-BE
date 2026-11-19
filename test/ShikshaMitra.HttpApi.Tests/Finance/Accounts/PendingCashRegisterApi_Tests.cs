using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Accounts.Activity;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeReceiptTests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceReceiptApiTestCollection.Name)]
public class PendingCashRegisterApi_Tests : ShikshaMitraApiTestBase
{
    public PendingCashRegisterApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory)
    {
        FeeCollectionTestHelper.DefaultAcademicYearId = ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId = ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId = ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId = ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
    }

    [Fact]
    public async Task Pending_cash_receipt_cannot_bypass_income_approval_through_receipt_detail()
    {
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
        var headId = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var dueId = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, headId, 100d);
        var input = FeeCollectionTestHelper.BuildCollectFeeInput(studentId, Factory.SeededAcademicYearId, headId, dueId, 100d);
        var collected = await Client.PostAsJsonAsync("/api/fee-collections", input, JsonOptions);
        collected.StatusCode.ShouldBe(HttpStatusCode.Created, await collected.Content.ReadAsStringAsync());
        var output = (await collected.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions))!;

        // Isolate the read boundary: pending operational entries must hide the receipt
        // fallback even though the official receipt itself is valid payment evidence.
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse("11111111-1111-1111-1111-111111111111")))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var entries = await scope.ServiceProvider.GetRequiredService<IRepository<Transaction, Guid>>()
                .GetListAsync(x => x.SourceId == output.Id);
            entries.ShouldNotBeEmpty();
            foreach (var entry in entries)
                typeof(Transaction).GetProperty(nameof(Transaction.IsConfirmed))!.SetValue(entry, false);
            await uow.CompleteAsync();
        }
        var year = Factory.SeededAcademicYearId;
        var register = await Client.GetFromJsonAsync<AccountsActivityPageDto>(
            $"/api/accounts/register?academicYearId={year}&direction=income&search={Uri.EscapeDataString(output.FeeReceiptNumber!)}", JsonOptions);
        register!.Items.ShouldBeEmpty();
        var detail = await Client.GetAsync($"/api/accounts/register/collection/{output.FeeReceiptId}?academicYearId={year}");
        detail.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Receipt download remains independently authorized and available.
        (await Client.GetAsync($"/api/v1/fee-receipts/{output.FeeReceiptId}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
