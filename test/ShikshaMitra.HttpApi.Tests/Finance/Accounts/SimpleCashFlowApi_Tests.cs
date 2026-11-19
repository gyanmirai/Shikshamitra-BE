using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Activity;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.FeeReceiptTests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[Collection(FinanceReceiptApiTestCollection.Name)]
public class SimpleCashFlowApi_Tests : ShikshaMitraApiTestBase
{
    public SimpleCashFlowApi_Tests(FinanceReceiptApiTestFactory factory) : base(factory)
    {
        FeeCollectionTestHelper.DefaultAcademicYearId = ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
        FeeCollectionTestHelper.DefaultMediumId = ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
        FeeCollectionTestHelper.DefaultStandardId = ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
        FeeCollectionTestHelper.DefaultDivisionId = ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
    }

    [Fact]
    public async Task Bulk_deposit_closes_year_pool_and_replays_after_balance_changes()
    {
        var year = Factory.SeededAcademicYearId;
        await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
        var category = await PostAsync(Client, $"/api/income-categories?academicYearId={year}", new { name = "Pool " + Guid.NewGuid().ToString("N") });
        for (var i = 0; i < 2; i++)
            await PostAsync(Client, $"/api/incomes?academicYearId={year}", new { idempotencyKey = Guid.NewGuid(), name = "Pool cash " + i,
                categoryId = category.GetProperty("id").GetGuid(), amount = 50m, paymentMethod = 0, paymentDate = DateTimeOffset.UtcNow,
                categoryTags = Array.Empty<string>() });
        var expenses = await PostAsync(Client, $"/api/expense-categories?academicYearId={year}", new { name = "Pool costs " + Guid.NewGuid().ToString("N") });
        var before = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}");
        var available = before.GetProperty("availableCash").GetDecimal();
        var overdraft = await Client.PostAsJsonAsync($"/api/expenses?academicYearId={year}", new { idempotencyKey = Guid.NewGuid(), name = "Pool overdraft",
            counterparty = "Supplier", categoryId = expenses.GetProperty("id").GetGuid(), amount = available + 1m,
            paymentMethod = 0, paymentDate = DateTimeOffset.UtcNow, categoryTags = Array.Empty<string>() }, JsonOptions);
        overdraft.IsSuccessStatusCode.ShouldBeFalse();
        (await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}")).GetProperty("availableCash").GetDecimal().ShouldBe(available);
        var expenseAmount = available - 25m;
        var expense = new { idempotencyKey = Guid.NewGuid(), name = "Pooled payment " + Guid.NewGuid(), counterparty = "Supplier",
            categoryId = expenses.GetProperty("id").GetGuid(), amount = expenseAmount, paymentMethod = 0, paymentDate = DateTimeOffset.UtcNow,
            categoryTags = Array.Empty<string>() };
        await PostAsync(Client, $"/api/expenses?academicYearId={year}", expense);
        await PostAsync(Client, $"/api/expenses?academicYearId={year}", expense);
        var filtered = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&search=does-not-match-{Guid.NewGuid()}");
        filtered.GetProperty("totalCount").GetInt32().ShouldBe(0);
        filtered.GetProperty("availableCash").GetDecimal().ShouldBe(25m);
        var indiaNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromMinutes(330));
        var monthPage = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&month={indiaNow.Month}&year={indiaNow.Year}");
        monthPage.GetProperty("availableCash").GetDecimal().ShouldBe(25m);
        var remainingPage = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&pageSize=100");
        var remainingId = remainingPage.GetProperty("items").EnumerateArray().First(x => x.GetProperty("remainingAmount").GetDecimal() > 0).GetProperty("id").GetGuid();
        var allocatedDetail = await ReadAsync(Client, $"/api/finance/cash-management/{remainingId}?academicYearId={year}");
        allocatedDetail.GetProperty("expenses").EnumerateArray().Single(x => x.GetProperty("name").GetString() == expense.name)
            .GetProperty("id").GetGuid().ShouldBe(expense.idempotencyKey);
        var (bankCreator, bank) = await ReviewerAndBankAsync();
        bankCreator.Dispose();
        using var actor = Factory.CreateClient();
        actor.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Accounts.CashManagement.Deposit");
        var key = Guid.NewGuid();
        using (actor)
        {
            async Task<HttpResponseMessage> Deposit(decimal expected)
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(year.ToString()), "academicYearId");
                form.Add(new StringContent(bank.ToString()), "bankAccountId");
                form.Add(new StringContent(expected.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedAmount");
                form.Add(new StringContent(key.ToString()), "idempotencyKey");
                form.Add(new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nsynthetic deposit slip")), "file", "deposit.pdf");
                return await actor.PostAsync("/api/finance/cash-management/bulk-deposit", form);
            }
            (await Deposit(available)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var first = await Deposit(25m);
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await first.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("amount").GetDecimal().ShouldBe(25m);
            (await Deposit(25m)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await Deposit(1m)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
        var after = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&pageSize=100");
        after.GetProperty("availableCash").GetDecimal().ShouldBe(0m);
        var detail = await ReadAsync(Client, $"/api/finance/cash-management/{remainingId}?academicYearId={year}");
        detail.GetProperty("supportingDocumentName").GetString().ShouldBe("deposit.pdf");
        var download = await Client.GetAsync($"/api/finance/cash-management/{remainingId}/supporting-document?academicYearId={year}");
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        // Simulate the original payer becoming inaccessible after a successful batch.
        // A same-total new source must not make this old key a fresh deposit.
        using (var serviceScope = Factory.Services.CreateScope())
        {
            var services = serviceScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
            using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var repo = services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.CashManagement.CashCollection, Guid>>();
            var hidden = await repo.GetAsync(remainingId);
            typeof(ShikshaMitra.Finance.Accounts.CashManagement.CashCollection).GetProperty("StudentId")!.SetValue(hidden, Guid.NewGuid());
            await repo.UpdateAsync(hidden);
            await uow.CompleteAsync();
        }
        (await Client.GetAsync($"/api/expenses/{expense.idempotencyKey}")).IsSuccessStatusCode.ShouldBeFalse();
        var hiddenExpenses = await ReadAsync(Client, $"/api/expenses?academicYearId={year}&search={Uri.EscapeDataString(expense.name)}");
        hiddenExpenses.GetProperty("totalCount").GetInt32().ShouldBe(0);
        var hiddenRegister = await ReadAsync(Client, $"/api/accounts/register?academicYearId={year}&direction=expense&search={Uri.EscapeDataString(expense.name)}");
        hiddenRegister.GetProperty("items").GetArrayLength().ShouldBe(0);
        await PostAsync(Client, $"/api/incomes?academicYearId={year}", new { idempotencyKey = Guid.NewGuid(), name = "Replacement pool cash",
            categoryId = category.GetProperty("id").GetGuid(), amount = 25m, paymentMethod = 0, paymentDate = DateTimeOffset.UtcNow,
            categoryTags = Array.Empty<string>() });
        using var replayForm = new MultipartFormDataContent();
        replayForm.Add(new StringContent(year.ToString()), "academicYearId");
        replayForm.Add(new StringContent(bank.ToString()), "bankAccountId");
        replayForm.Add(new StringContent("25"), "expectedAmount");
        replayForm.Add(new StringContent(key.ToString()), "idempotencyKey");
        replayForm.Add(new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\nsynthetic deposit slip")), "file", "deposit.pdf");
        (await Client.PostAsync("/api/finance/cash-management/bulk-deposit", replayForm)).IsSuccessStatusCode.ShouldBeFalse();
        (await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}")).GetProperty("availableCash").GetDecimal().ShouldBe(25m);
    }

    [Fact]
    public async Task Receipt_pending_expense_then_net_approval_conserves_cash_and_original_receipt()
    {
        var year = Factory.SeededAcademicYearId;
        await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
        var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
        var head = await FeeCollectionTestHelper.CreateFeeHeadAsync(Client);
        var due = await FeeCollectionTestHelper.SeedDueFeeAsync(Factory.Services, studentId, head, 10000d);
        var collectionInput = FeeCollectionTestHelper.BuildCollectFeeInput(studentId, year, head, due, 10000d);
        var collection = await PostAsync(Client, "/api/fee-collections",
            collectionInput);
        var sourceId = collection.GetProperty("id").GetGuid();
        var receiptId = collection.GetProperty("feeReceiptId").GetGuid();
        var page = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&pageSize=100");
        var cash = page.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("sourceId").GetGuid() == sourceId);
        var cashId = cash.GetProperty("id").GetGuid();
        cash.GetProperty("status").GetInt32().ShouldBe(0);
        cash.GetProperty("remainingAmount").GetDecimal().ShouldBe(10000m);
        await AssertIncomeAsync(sourceId, 0m);
        (await Client.GetAsync($"/api/v1/fee-receipts/{receiptId}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (reviewer, bankId) = await ReviewerAndBankAsync();
        var approve = new { academicYearId = year, bankAccountId = bankId, countedAmount = 10000m,
            concurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() };
        // A valid source ID and action grant must not bypass tenant/year/payer scope.
        (await Client.GetAsync($"/api/finance/cash-management/{cashId}?academicYearId={Guid.NewGuid()}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using (var foreignTenant = Factory.CreateClient())
        {
            foreignTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
            (await foreignTenant.GetAsync($"/api/finance/cash-management/{cashId}?academicYearId={year}"))
                .IsSuccessStatusCode.ShouldBeFalse();
            (await foreignTenant.PostAsJsonAsync($"/api/finance/cash-management/{cashId}/approve", approve, JsonOptions))
                .IsSuccessStatusCode.ShouldBeFalse();
        }
        using (var unscoped = CreateClientAs(Guid.NewGuid()))
        {
            unscoped.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "");
            (await unscoped.GetAsync($"/api/finance/cash-management/{cashId}?academicYearId={year}"))
                .IsSuccessStatusCode.ShouldBeFalse();
            (await unscoped.GetAsync($"/api/finance/cash-management/options?academicYearId={year}&operation=expense"))
                .IsSuccessStatusCode.ShouldBeFalse();
        }
        (await Client.PostAsJsonAsync($"/api/finance/cash-management/{cashId}/approve", approve, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();
        using (var expenseWriter = Factory.CreateClient())
        {
            expenseWriter.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
                "Finance.Accounts.ExpenseTracking.Add");
            var options = await ReadAsync(expenseWriter, $"/api/finance/cash-management/options?academicYearId={year}&operation=expense");
            options.GetProperty("cashSources").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == cashId).ShouldBeTrue();
            options.GetProperty("bankAccounts").GetArrayLength().ShouldBe(0);
            (await expenseWriter.GetAsync($"/api/finance/cash-management/{cashId}?academicYearId={year}"))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        var category = await PostAsync(Client, $"/api/expense-categories?academicYearId={year}", new { name = "Cash stationery " + Guid.NewGuid().ToString("N") });
        var at = DateTimeOffset.UtcNow;
        var expense = new { idempotencyKey = Guid.NewGuid(), name = "Stationery " + cashId, counterparty = "Synthetic supplier",
            categoryId = category.GetProperty("id").GetGuid(), amount = 2000m, paymentMethod = 0, paymentDate = at,
            categoryTags = Array.Empty<string>(), cashCollectionId = cashId,
            cashConcurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), paymentReference = "BILL-" + cashId };
        await PostAsync(Client, $"/api/expenses?academicYearId={year}", expense);
        await PostAsync(Client, $"/api/expenses?academicYearId={year}", expense);
        cash = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        cash.GetProperty("spentAmount").GetDecimal().ShouldBe(2000m);
        cash.GetProperty("expenses").GetArrayLength().ShouldBe(1);
        cash.GetProperty("remainingAmount").GetDecimal().ShouldBe(8000m);
        var pendingSummary = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&search={cash.GetProperty("reference").GetString()}");
        pendingSummary.GetProperty("summary").GetProperty("pendingApproval").GetDecimal().ShouldBe(8000m);
        var filterJson = Uri.EscapeDataString("""[{"column":"remainingAmount","type":"greaterThan","value":9000}]""");
        var filteredUrl = $"/api/finance/cash-management?academicYearId={year}&search={Uri.EscapeDataString(cash.GetProperty("reference").GetString()!)}&filters={filterJson}";
        var filtered = await ReadAsync(Client, filteredUrl);
        filtered.GetProperty("totalCount").GetInt32().ShouldBe(0);
        filtered.GetProperty("summary").GetProperty("remaining").GetDecimal().ShouldBe(0m);
        var counts = pendingSummary.GetProperty("statusCounts");
        counts.GetProperty("pending").GetInt32().ShouldBe(1);
        counts.GetProperty("approved").GetInt32().ShouldBe(0);
        var invalidFilter = Uri.EscapeDataString("""[{"column":"tenantId","type":"equals","value":"other"}]""");
        (await Client.GetAsync($"/api/finance/cash-management?academicYearId={year}&filters={invalidFilter}"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await AssertIncomeAsync(sourceId, 0m);
        var staleApproval = await reviewer.PostAsJsonAsync($"/api/finance/cash-management/{cashId}/approve", approve, JsonOptions);
        staleApproval.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var wrongCount = await reviewer.PostAsJsonAsync($"/api/finance/cash-management/{cashId}/approve", new {
            academicYearId = year, bankAccountId = bankId, countedAmount = 10000m,
            concurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() }, JsonOptions);
        (await wrongCount.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("errorCode").GetString()
            .ShouldBe("Finance:CashManagement:CountMismatch");
        var netApproval = new { academicYearId = year, bankAccountId = bankId, countedAmount = 8000m,
            concurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() };
        cash = await PostAsync(reviewer, $"/api/finance/cash-management/{cashId}/approve", netApproval);
        var replay = await PostAsync(reviewer, $"/api/finance/cash-management/{cashId}/approve", netApproval);
        replay.GetProperty("concurrencyStamp").GetString().ShouldBe(cash.GetProperty("concurrencyStamp").GetString());
        cash.GetProperty("amount").GetDecimal().ShouldBe(10000m);
        cash.GetProperty("spentAmount").GetDecimal().ShouldBe(2000m);
        cash.GetProperty("depositedAmount").GetDecimal().ShouldBe(8000m);
        cash.GetProperty("remainingAmount").GetDecimal().ShouldBe(0m);
        cash.GetProperty("inTransitAmount").GetDecimal().ShouldBe(0m);
        cash.GetProperty("canDeposit").GetBoolean().ShouldBeFalse();
        var deposit = cash.GetProperty("deposits").EnumerateArray().Single();
        deposit.GetProperty("amount").GetDecimal().ShouldBe(8000m);
        deposit.GetProperty("creditedAmount").GetDecimal().ShouldBe(8000m);
        var optionsAfter = await ReadAsync(Client, $"/api/finance/cash-management/options?academicYearId={year}&operation=expense");
        optionsAfter.GetProperty("cashSources").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == cashId).ShouldBeFalse();
        var paidAgain = await Client.PostAsJsonAsync($"/api/expenses?academicYearId={year}", new {
            idempotencyKey = Guid.NewGuid(), name = "After approval", counterparty = "Supplier",
            categoryId = category.GetProperty("id").GetGuid(), amount = 1m, paymentMethod = 0, paymentDate = DateTimeOffset.UtcNow,
            categoryTags = Array.Empty<string>(), cashCollectionId = cashId,
            cashConcurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), paymentReference = "AFTER" }, JsonOptions);
        paidAgain.IsSuccessStatusCode.ShouldBeFalse();
        await AssertIncomeAsync(sourceId, 8000m);
        var paidRegister = await Client.GetFromJsonAsync<AccountsActivityPageDto>($"/api/accounts/register?academicYearId={year}&direction=expense&search={Uri.EscapeDataString("Stationery " + cashId)}", JsonOptions);
        paidRegister!.Items.Sum(x => x.Amount).ShouldBe(2000m);
        var sourceRetry = await PostAsync(Client, "/api/fee-collections", collectionInput);
        sourceRetry.GetProperty("id").GetGuid().ShouldBe(sourceId);
        var official = await ReadAsync(Client, $"/api/v1/fee-receipts/{receiptId}");
        official.GetProperty("totalPaidAmount").GetDecimal().ShouldBe(10000m);
        (await Client.GetAsync($"/api/v1/fee-receipts/{receiptId}/pdf")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task AssertIncomeAsync(Guid sourceId, decimal expected)
    {
        var register = await Client.GetFromJsonAsync<AccountsActivityPageDto>(
            $"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction=income&pageSize=100", JsonOptions);
        register!.Items.Where(x => x.SourceId == sourceId).Sum(x => x.Amount).ShouldBe(expected);
    }

    [Theory]
    [InlineData(20, 80)]
    [InlineData(100, 0)]
    public async Task Manual_cash_income_original_retry_survives_net_or_zero_finalization(decimal spent, decimal net)
    {
        var year = Factory.SeededAcademicYearId;
        await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
        var category = await PostAsync(Client, $"/api/income-categories?academicYearId={year}", new { name = "Cash donation " + Guid.NewGuid().ToString("N")[..8] });
        var paidAt = await CashTestDates.InYearAsync(Factory.Services, year);
        var body = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["idempotencyKey"] = Guid.NewGuid(), ["name"] = "Synthetic cash donation " + Guid.NewGuid().ToString("N"),
            ["counterparty"] = "Synthetic donor", ["categoryId"] = category.GetProperty("id").GetGuid(),
            ["amount"] = 100m, ["paymentMethod"] = 0, ["paymentDate"] = paidAt,
            ["categoryTags"] = Array.Empty<string>(), ["paymentReference"] = "DONATION-" + Guid.NewGuid().ToString("N")
        };
        var url = $"/api/incomes?academicYearId={year}";
        var futureCash = new System.Collections.Generic.Dictionary<string, object?>(body)
            { ["idempotencyKey"] = Guid.NewGuid(), ["paymentDate"] = DateTimeOffset.UtcNow.AddHours(1) };
        var futureResponse = await Client.PostAsJsonAsync(url, futureCash, JsonOptions);
        futureResponse.IsSuccessStatusCode.ShouldBeFalse();
        (await futureResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("errorCode").GetString()
            .ShouldBe("Finance:CashManagement:InvalidEvidence");
        await PostAsync(Client, url, body);
        await AssertChangedFieldsDeniedAsync();
        var cashPage = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&search={Uri.EscapeDataString((string)body["paymentReference"]!)}");
        var cash = cashPage.GetProperty("items").EnumerateArray().Single();
        var (reviewer, bankId) = await ReviewerAndBankAsync();
        var expenseCategory = await PostAsync(Client, $"/api/expense-categories?academicYearId={year}", new { name = "Net expense " + Guid.NewGuid().ToString("N")[..8] });
        var cashId = cash.GetProperty("id").GetGuid();
        await PostAsync(Client, $"/api/expenses?academicYearId={year}", new {
            idempotencyKey = Guid.NewGuid(), name = "Pending cash paid expense", counterparty = "Supplier",
            categoryId = expenseCategory.GetProperty("id").GetGuid(), amount = spent, paymentMethod = 0,
            paymentDate = DateTimeOffset.UtcNow, categoryTags = Array.Empty<string>(), cashCollectionId = cashId,
            cashConcurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), paymentReference = "BILL" });
        cash = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        await PostAsync(reviewer, $"/api/finance/cash-management/{cash.GetProperty("id").GetGuid()}/approve", new {
            academicYearId = year, bankAccountId = bankId, countedAmount = net,
            concurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() });
        // Approval adds the destination bank to stored Income; original cash retries
        // still send no bank or cash routing fields.
        await PostAsync(Client, url, body);
        await AssertChangedFieldsDeniedAsync();
        cash = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        cash.GetProperty("depositedAmount").GetDecimal().ShouldBe(net);
        cash.GetProperty("remainingAmount").GetDecimal().ShouldBe(0m);
        cash.GetProperty("deposits").GetArrayLength().ShouldBe(net == 0 ? 0 : 1);
        var register = await Client.GetFromJsonAsync<AccountsActivityPageDto>($"/api/accounts/register?academicYearId={year}&direction=income&pageSize=100&search={Uri.EscapeDataString((string)body["name"]!)}", JsonOptions);
        register!.Items.Sum(x => x.Amount).ShouldBe(net);
        register.Items.Count.ShouldBe(net == 0 ? 0 : 1);
        var incomes = await ReadAsync(Client, $"/api/incomes?academicYearId={year}&search={Uri.EscapeDataString((string)body["name"]!)}");
        incomes.GetProperty("items").GetArrayLength().ShouldBe(net == 0 ? 0 : 1);
        if (net == 0)
        {
            (await Client.GetAsync($"/api/incomes/{body["idempotencyKey"]}")).IsSuccessStatusCode.ShouldBeFalse();
            (await Client.GetAsync($"/api/accounts/register/income/{body["idempotencyKey"]}?academicYearId={year}"))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        var changedAmount = new System.Collections.Generic.Dictionary<string, object?>(body) { ["amount"] = net == 0 ? 1m : net };
        (await Client.PostAsJsonAsync(url, changedAmount, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();


        async Task AssertChangedFieldsDeniedAsync()
        {
            foreach (var field in new[] { "bankAccountId", "cashCollectionId", "cashConcurrencyStamp" })
            {
                var changed = new System.Collections.Generic.Dictionary<string, object?>(body)
                    { [field] = field == "cashConcurrencyStamp" ? "changed-stamp" : Guid.NewGuid() };
                (await Client.PostAsJsonAsync(url, changed, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse(field);
            }
            await PostAsync(Client, url, body);
        }
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super-admin")]
    public async Task Administrator_self_approval_uses_current_membership_and_requires_exact_permission(string roleName)
    {
        var year = Factory.SeededAcademicYearId;
        await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
        var (actor, bankId) = await ReviewerAndBankAsync();
        using var actorClient = actor;
        var actorId = Guid.Parse(actor.DefaultRequestHeaders.GetValues(TestAuthHandler.TestUserIdHeader).Single());
        var reference = "ADMIN-CASH-" + Guid.NewGuid().ToString("N");
        var category = await PostAsync(Client, $"/api/income-categories?academicYearId={year}",
            new { name = "Admin cash " + Guid.NewGuid().ToString("N")[..8] });
        await PostAsync(actor, $"/api/incomes?academicYearId={year}", new {
            idempotencyKey = Guid.NewGuid(), name = reference, counterparty = "Synthetic donor",
            categoryId = category.GetProperty("id").GetGuid(), amount = 100m, paymentMethod = 0,
            paymentDate = await CashTestDates.InYearAsync(Factory.Services, year),
            categoryTags = Array.Empty<string>(), paymentReference = reference });
        var page = await ReadAsync(actor, $"/api/finance/cash-management?academicYearId={year}&search={reference}");
        var cash = page.GetProperty("items").EnumerateArray().Single();
        var cashId = cash.GetProperty("id").GetGuid();
        var endpoint = $"/api/finance/cash-management/{cashId}";
        var command = new { academicYearId = year, bankAccountId = bankId, countedAmount = 100m,
            concurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() };

        // Test auth claims already say admin; database membership alone controls this exception.
        cash.GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        var denied = await actor.PostAsJsonAsync(endpoint + "/approve", command, JsonOptions);
        denied.IsSuccessStatusCode.ShouldBeFalse();
        (await denied.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("errorCode").GetString()
            .ShouldBe("Finance:CashManagement:IndependentChecker");

        await SetAdministratorRoleAsync(true);
        (await ReadAsync(actor, endpoint + $"?academicYearId={year}"))
            .GetProperty("canApprove").GetBoolean().ShouldBeTrue();
        actor.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "Finance.Accounts.CashManagement.View");
        (await actor.PostAsJsonAsync(endpoint + "/approve", command, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        actor.DefaultRequestHeaders.Remove(AlwaysGrantPermissionChecker.TestPermissionsHeader);

        await SetAdministratorRoleAsync(false);
        (await ReadAsync(actor, endpoint + $"?academicYearId={year}"))
            .GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        (await actor.PostAsJsonAsync(endpoint + "/approve", command, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();

        await SetAdministratorRoleAsync(true);
        var approved = await PostAsync(actor, endpoint + "/approve", command);
        approved.GetProperty("status").GetInt32().ShouldBe(1);
        approved.GetProperty("approvedBy").GetGuid().ShouldBe(actorId);
        approved.GetProperty("bankAccountId").GetGuid().ShouldBe(bankId);
        var retried = await PostAsync(actor, endpoint + "/approve", command);
        retried.GetProperty("concurrencyStamp").GetString().ShouldBe(approved.GetProperty("concurrencyStamp").GetString());
        await AssertIncomeAsync(cash.GetProperty("sourceId").GetGuid(), 100m);

        async Task SetAdministratorRoleAsync(bool assign)
        {
            using var serviceScope = Factory.Services.CreateScope();
            var services = serviceScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
            using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var roles = services.GetRequiredService<IdentityRoleManager>();
            if (await roles.FindByNameAsync(roleName) == null)
                (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, Guid.Parse(TestAuthHandler.TestTenantId))))
                    .Succeeded.ShouldBeTrue();
            var users = services.GetRequiredService<IdentityUserManager>();
            var user = await users.GetByIdAsync(actorId);
            var result = assign ? await users.AddToRoleAsync(user, roleName) : await users.RemoveFromRoleAsync(user, roleName);
            result.Succeeded.ShouldBeTrue();
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task Multiple_pending_income_rows_allocate_exact_paise_without_rewriting_posted_history()
    {
        var year = Factory.SeededAcademicYearId;
        await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
        var (reviewer, bankId) = await ReviewerAndBankAsync();
        using var reviewerClient = reviewer;
        var category = await PostAsync(Client, $"/api/income-categories?academicYearId={year}", new { name = "Paise " + Guid.NewGuid().ToString("N")[..8] });
        var cashId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var at = await CashTestDates.InYearAsync(Factory.Services, year);
        var prefix = "collection:" + sourceId.ToString("N") + ":0:";
        using (var serviceScope = Factory.Services.CreateScope())
        {
            var services = serviceScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
            using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var cash = new ShikshaMitra.Finance.Accounts.CashManagement.CashCollection(cashId, Guid.Parse(TestAuthHandler.TestTenantId), year,
                sourceId, prefix, null, "PAISE-" + sourceId, "Synthetic payer", .04m, at, Guid.Parse(TestAuthHandler.TestUserId), null, null);
            cash.Spend(Guid.NewGuid(), "Paise expense", .02m, at);
            await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.CashManagement.CashCollection, Guid>>().InsertAsync(cash);
            var repository = services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>();
            // Reverse insertion order deliberately: allocation follows source identity, never database order.
            for (var i = 3; i >= 0; i--)
                await repository.InsertAsync(ShikshaMitra.Finance.Accounts.Incomes.Transaction.Automatic(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
                    year, category.GetProperty("id").GetGuid(), "Paise", null, null, "Paise income", .01m, 0, at.UtcDateTime,
                    ShikshaMitra.Finance.Accounts.Transactions.TransactionType.Income, prefix + i, sourceId, null, confirmed: false));
            await uow.CompleteAsync();
        }
        var detail = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        var command = new { academicYearId = year, bankAccountId = bankId, countedAmount = .02m,
            concurrencyStamp = detail.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() };
        await PostAsync(reviewer, $"/api/finance/cash-management/{cashId}/approve", command);
        await PostAsync(reviewer, $"/api/finance/cash-management/{cashId}/approve", command);
        await AssertIncomeAsync(sourceId, .02m);
        using (var serviceScope = Factory.Services.CreateScope())
        {
            var services = serviceScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
            using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            await services.GetRequiredService<ShikshaMitra.Finance.Accounts.AutomaticTransactionManager>().ConfirmAsync(sourceId);
            var rows = await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>().GetListAsync(x => x.SourceId == sourceId);
            rows.Count.ShouldBe(4);
            var sorted = rows.OrderBy(x => x.SourceKey, StringComparer.Ordinal).ToArray();
            sorted.Select(x => x.Amount).ToArray().ShouldBe(new[] { .01m, .01m, 0m, 0m });
            rows.All(x => x.Amount is >= 0m and <= .01m).ShouldBeTrue();
            rows.Count(x => x.IsConfirmed).ShouldBe(2);
            rows.All(x => x.ExtraProperties.ContainsKey("OriginalCashGrossAmount")).ShouldBeTrue();
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task Legacy_approved_cash_keeps_gross_income_partial_deposit_and_independent_credit()
    {
        var year = Factory.SeededAcademicYearId;
        await CashTestDates.EnsureCurrentYearAsync(Client, year, JsonOptions);
        var (reviewer, bankId) = await ReviewerAndBankAsync();
        using var reviewerClient = reviewer;
        var cashId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var category = await PostAsync(Client, $"/api/income-categories?academicYearId={year}", new { name = "Legacy " + Guid.NewGuid().ToString("N")[..8] });
        var at = await CashTestDates.InYearAsync(Factory.Services, year);
        using (var serviceScope = Factory.Services.CreateScope())
        {
            var services = serviceScope.ServiceProvider;
            using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
            using var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var cash = new ShikshaMitra.Finance.Accounts.CashManagement.CashCollection(cashId, Guid.Parse(TestAuthHandler.TestTenantId), year,
                sourceId, "collection:" + sourceId.ToString("N") + ":0:", null, "LEGACY-" + sourceId, "Legacy payer", 100m, at,
                Guid.Parse(TestAuthHandler.TestUserId), null, null);
            typeof(ShikshaMitra.Finance.Accounts.CashManagement.CashCollection).GetProperty("Status")!.SetValue(cash, 1);
            typeof(ShikshaMitra.Finance.Accounts.CashManagement.CashCollection).GetProperty("BankAccountId")!.SetValue(cash, bankId);
            await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.CashManagement.CashCollection, Guid>>().InsertAsync(cash);
            await services.GetRequiredService<IRepository<ShikshaMitra.Finance.Accounts.Incomes.Transaction, Guid>>().InsertAsync(
                ShikshaMitra.Finance.Accounts.Incomes.Transaction.Automatic(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), year,
                    category.GetProperty("id").GetGuid(), "Legacy income", bankId, "Legacy bank", "Legacy gross income", 100m, 0,
                    at.UtcDateTime, ShikshaMitra.Finance.Accounts.Transactions.TransactionType.Income,
                    "collection:" + sourceId.ToString("N") + ":0:0", sourceId, null));
            await uow.CompleteAsync();
        }
        var detail = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        var options = await ReadAsync(Client, $"/api/finance/cash-management/options?academicYearId={year}&operation=deposit");
        options.GetProperty("cashSources").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == cashId).ShouldBeTrue();
        var dispatch = new { academicYearId = year, amount = 60m, depositDate = DateTimeOffset.UtcNow,
            reference = "LEGACY-SLIP", concurrencyStamp = detail.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() };
        detail = await PostAsync(Client, $"/api/finance/cash-management/{cashId}/deposits", dispatch);
        await PostAsync(Client, $"/api/finance/cash-management/{cashId}/deposits", dispatch);
        var depositId = detail.GetProperty("deposits").EnumerateArray().Single().GetProperty("id").GetGuid();
        var credit = new { academicYearId = year, amount = 50m, creditDate = DateTimeOffset.UtcNow,
            reference = "LEGACY-CREDIT", concurrencyStamp = detail.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() };
        (await Client.PostAsJsonAsync($"/api/finance/cash-management/{cashId}/deposits/{depositId}/confirm", credit, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();
        detail = await PostAsync(reviewer, $"/api/finance/cash-management/{cashId}/deposits/{depositId}/confirm", credit);
        detail.GetProperty("remainingAmount").GetDecimal().ShouldBe(40m);
        detail.GetProperty("inTransitAmount").GetDecimal().ShouldBe(10m);
        detail = await PostAsync(reviewer, $"/api/finance/cash-management/{cashId}/deposits/{depositId}/confirm", new {
            academicYearId = year, amount = 10m, creditDate = DateTimeOffset.UtcNow, reference = "LEGACY-FINAL",
            concurrencyStamp = detail.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() });
        detail.GetProperty("depositedAmount").GetDecimal().ShouldBe(60m);
        detail.GetProperty("remainingAmount").GetDecimal().ShouldBe(40m);
        detail.GetProperty("inTransitAmount").GetDecimal().ShouldBe(0m);
        await AssertIncomeAsync(sourceId, 100m);
        var expenses = await ReadAsync(Client, $"/api/finance/cash-management/options?academicYearId={year}&operation=expense");
        expenses.GetProperty("cashSources").EnumerateArray().Any(x => x.GetProperty("id").GetGuid() == cashId).ShouldBeTrue();
        var (newBankCreator, newBankId) = await ReviewerAndBankAsync();
        newBankCreator.Dispose();
        var yearPool = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(year.ToString()), "academicYearId");
        form.Add(new StringContent(newBankId.ToString()), "bankAccountId");
        form.Add(new StringContent(yearPool.GetProperty("availableCash").GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedAmount");
        form.Add(new StringContent(Guid.NewGuid().ToString()), "idempotencyKey");
        (await Client.PostAsync("/api/finance/cash-management/bulk-deposit", form)).StatusCode.ShouldBe(HttpStatusCode.OK);
        detail = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        detail.GetProperty("bankAccountId").GetGuid().ShouldBe(newBankId);
        var deposits = detail.GetProperty("deposits").EnumerateArray().ToArray();
        deposits.Single(x => x.GetProperty("id").GetGuid() == depositId).GetProperty("bankAccountId").GetGuid().ShouldBe(bankId);
        deposits.Single(x => x.GetProperty("id").GetGuid() != depositId).GetProperty("bankAccountId").GetGuid().ShouldBe(newBankId);
        await AssertIncomeAsync(sourceId, 100m);
    }

    [Fact]
    public async Task Prior_year_cash_cannot_record_todays_approval_as_a_bank_deposit()
    {
        var previous = DateTime.UtcNow.Year - 2;
        var yearRow = await PostAsync(Client, "/api/app/academic-year", new {
            name = "Prior cash " + Guid.NewGuid().ToString("N")[..8],
            startYear = new DateTime(previous, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            endYear = new DateTime(previous, 12, 31, 0, 0, 0, DateTimeKind.Utc) });
        var year = yearRow.GetProperty("id").GetGuid();
        var category = await PostAsync(Client, $"/api/income-categories?academicYearId={year}", new { name = "Prior cash income" });
        var reference = "PRIOR-" + Guid.NewGuid().ToString("N");
        await PostAsync(Client, $"/api/incomes?academicYearId={year}", new {
            idempotencyKey = Guid.NewGuid(), name = reference, counterparty = "Synthetic donor",
            categoryId = category.GetProperty("id").GetGuid(), amount = 100m, paymentMethod = 0,
            paymentDate = new DateTimeOffset(previous, 6, 1, 12, 0, 0, TimeSpan.Zero),
            categoryTags = Array.Empty<string>(), paymentReference = reference });
        var page = await ReadAsync(Client, $"/api/finance/cash-management?academicYearId={year}&search={reference}");
        var cash = page.GetProperty("items").EnumerateArray().Single();
        var cashId = cash.GetProperty("id").GetGuid();
        var (reviewer, bankId) = await ReviewerAndBankAsync(year);
        using var reviewerClient = reviewer;
        var approval = await reviewer.PostAsJsonAsync($"/api/finance/cash-management/{cashId}/approve", new {
            academicYearId = year, bankAccountId = bankId, countedAmount = 100m,
            concurrencyStamp = cash.GetProperty("concurrencyStamp").GetString(), idempotencyKey = Guid.NewGuid() }, JsonOptions);
        approval.IsSuccessStatusCode.ShouldBeFalse();
        (await approval.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("errorCode").GetString()
            .ShouldBe("Finance:CashManagement:InvalidEvidence");
        var after = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        after.GetProperty("status").GetInt32().ShouldBe(0);
        after.GetProperty("bankAccountId").ValueKind.ShouldBe(JsonValueKind.Null);
        after.GetProperty("deposits").GetArrayLength().ShouldBe(0);
        after.GetProperty("remainingAmount").GetDecimal().ShouldBe(100m);
        var register = await ReadAsync(Client, $"/api/accounts/register?academicYearId={year}&direction=income&search={reference}");
        register.GetProperty("items").GetArrayLength().ShouldBe(0);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(year.ToString()), "academicYearId");
        form.Add(new StringContent(bankId.ToString()), "bankAccountId");
        form.Add(new StringContent("100"), "expectedAmount");
        form.Add(new StringContent(Guid.NewGuid().ToString()), "idempotencyKey");
        (await Client.PostAsync("/api/finance/cash-management/bulk-deposit", form)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var deposited = await ReadAsync(Client, $"/api/finance/cash-management/{cashId}?academicYearId={year}");
        deposited.GetProperty("remainingAmount").GetDecimal().ShouldBe(0m);
    }

    private async Task<(HttpClient Client, Guid BankId)> ReviewerAndBankAsync(Guid? academicYearId = null)
    {
        var userId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        using (services.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            (await services.GetRequiredService<IdentityUserManager>().CreateAsync(
                new IdentityUser(userId, "cash-reviewer-" + userId.ToString("N"), userId + "@example.test", tenantId))).Succeeded.ShouldBeTrue();
            await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
                new UserScopeAssignment(Guid.NewGuid(), tenantId, userId: userId, description: "Synthetic cash reviewer scope"));
            await services.GetRequiredService<IRepository<BankAccount, Guid>>().InsertAsync(new BankAccount(
                bankId, academicYearId ?? Factory.SeededAcademicYearId, "Cash acceptance bank", "Synthetic branch", null, "School",
                Enum.GetValues<BankAccountType>().First(), BitConverter.ToUInt64(bankId.ToByteArray(), 0).ToString(),
                "Cash workflow test", "TEST0001234", null, tenantId: tenantId));
            await uow.CompleteAsync();
        }
        return (CreateClientAs(userId), bankId);
    }

    private static async Task<JsonElement> PostAsync(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body, JsonOptions);
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }
}
