using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Accounts.Activity;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;
[Collection(FinanceRoutingApiTestCollection.Name)]
public class AccountsRegisterApi_Tests(FinanceRoutingApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
    [Theory]
    [InlineData("income", "Finance.Accounts.ExpenseTracking.View")]
    [InlineData("expense", "Finance.Accounts.IncomeTracking.View")]
    public async Task Direction_requires_its_exact_view_permission(string direction, string permission)
    {
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
        (await client.GetAsync($"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction={direction}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/accounts/register/{direction}/{Guid.NewGuid()}?academicYearId={Factory.SeededAcademicYearId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
    [Theory]
    [InlineData("income", "Finance.Accounts.IncomeTracking.Add")]
    [InlineData("expense", "Finance.Accounts.ExpenseTracking.Add")]
    public async Task Create_lookup_accepts_owning_add_without_master_view(string direction, string permission)
    {
        await SeedScopeAsync();
        using var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permission);
        var response = await client.GetAsync($"/api/finance-routing-options?academicYearId={Factory.SeededAcademicYearId}&operation={direction}-entry");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("income", "income-categories", "incomes")]
    [InlineData("expense", "expense-categories", "expenses")]
    public async Task Manual_entry_month_search_and_selected_record_detail_are_consistent(string direction, string categoryRoute, string route)
    {
        var at = await SeedScopeAsync();
        var categoryResponse = await Client.PostAsJsonAsync($"/api/{categoryRoute}?academicYearId={Factory.SeededAcademicYearId}", new { name = $"Reg-{Guid.NewGuid():N}" });
        categoryResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await categoryResponse.Content.ReadAsStringAsync());
        var category = await categoryResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var reference = $"Manual {Guid.NewGuid():N}";
        var requestKey = Guid.NewGuid();
        var body = new { idempotencyKey = requestKey, name = reference, counterparty = "Parent association", categoryId = category.GetProperty("id").GetGuid(), amount = 12.34m, paymentMethod = 0, paymentDate = at, paymentReference = "REF-123", categoryTags = Array.Empty<string>() };
        var created = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", body);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var value = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = value.GetProperty("data").GetProperty("id").GetGuid();
        id.ShouldBe(requestKey);
        var replay = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", body);
        replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        (await replay.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("data").GetProperty("id").GetGuid().ShouldBe(id);
        var calendarDate = at.AddMinutes(330);
        var url = $"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction={direction}&search={reference}&month={calendarDate.Month}&year={calendarDate.Year}";
        var listed = await Client.GetAsync(url);
        listed.StatusCode.ShouldBe(HttpStatusCode.OK, await listed.Content.ReadAsStringAsync());
        var page = await listed.Content.ReadFromJsonAsync<AccountsActivityPageDto>(JsonOptions);
        page!.TotalCount.ShouldBe(1); page.Items.Single().Amount.ShouldBe(12.34m);
        var detailResponse = await Client.GetAsync($"/api/accounts/register/{direction}/{id}?academicYearId={Factory.SeededAcademicYearId}");
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
        var detail = await detailResponse.Content.ReadFromJsonAsync<AccountsActivityDetailDto>(JsonOptions);
        detail!.Reference.ShouldBe(reference); detail.Counterparty.ShouldBe("Parent association");
        detail.Payments.Single().Reference.ShouldBe("REF-123"); detail.Amount.ShouldBe(12.34m);
        var foreignYear = await Client.GetAsync($"/api/accounts/register/{direction}/{id}?academicYearId={Guid.NewGuid()}");
        foreignYear.IsSuccessStatusCode.ShouldBeFalse();
        var opposite = direction == "income" ? "expense" : "income";
        (await Client.GetAsync($"/api/accounts/register/{opposite}/{id}?academicYearId={Factory.SeededAcademicYearId}")).IsSuccessStatusCode.ShouldBeFalse();
        var second = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new {
            idempotencyKey = Guid.NewGuid(), name = reference + " second", counterparty = "Another payer", categoryId = category.GetProperty("id").GetGuid(),
            amount = 5m, paymentMethod = 0, paymentDate = at.AddMinutes(1), categoryTags = Array.Empty<string>() });
        second.EnsureSuccessStatusCode();
        var firstSorted = await ReadPageAsync(url + "&sortBy=amount&sortDescending=false&pageSize=1");
        firstSorted.TotalCount.ShouldBe(2); firstSorted.Items.Single().Amount.ShouldBe(5m);
        (await ReadPageAsync(url + "&sortBy=amount&sortDescending=false&pageSize=1&page=2")).Items.Single().Amount.ShouldBe(12.34m);
        var amountFiltered = await ReadPageAsync(url + "&pageSize=1" + ColumnFilters(
            new { column = "amount", type = "between", value = new { min = 5m, max = 10m } },
            new { column = "source", type = "equals", value = direction == "income" ? "Income" : "Expense" },
            new { column = "sourceStatus", type = "equals", value = direction == "income" ? "Received" : "Paid" }));
        amountFiltered.TotalCount.ShouldBe(1);
        amountFiltered.Items.Single().Amount.ShouldBe(5m);
        var partyFiltered = await ReadPageAsync(url + "&pageSize=1" + ColumnFilters(
            new { column = "counterparty", type = "contains", value = "Another payer" }));
        partyFiltered.TotalCount.ShouldBe(1);
        partyFiltered.Items.Single().Counterparty.ShouldBe("Another payer");
        partyFiltered.Items.Single().StudentId.ShouldBeNull();
        partyFiltered.Items.Single().GrNumber.ShouldBeNull();
        partyFiltered.Items.Single().ClassDivision.ShouldBeEmpty();
        (await ReadPageAsync(url + ColumnFilters(new { column = "studentIdentity", type = "contains", value = "Parent association" })))
            .TotalCount.ShouldBe(0);
        var alternatives = ColumnFilters(
            new { column = "counterparty", type = "contains", value = "Parent association" },
            new { column = "counterparty", type = "contains", value = "Another payer" },
            new { column = "occurredAt", type = "equals", value = $"{calendarDate:yyyy-MM-dd}T00:00:00+05:30" });
        var pagedFilters = await ReadPageAsync(url + "&sortBy=amount&sortDescending=false&pageSize=1&page=2" + alternatives);
        pagedFilters.TotalCount.ShouldBe(2);
        pagedFilters.Items.Single().Amount.ShouldBe(12.34m);
        (await Client.GetAsync(url + ColumnFilters(new { column = "actions", type = "contains", value = "View" })))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var invalid = await Client.GetAsync(url.Replace($"month={calendarDate.Month}", "month=13"));
        invalid.IsSuccessStatusCode.ShouldBeFalse();
        var missingBank = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new { name = reference, categoryId = category.GetProperty("id").GetGuid(), amount = 12.34m, paymentMethod = 3, paymentDate = at });
        missingBank.IsSuccessStatusCode.ShouldBeFalse();
        var invalidMoney = await Client.PostAsJsonAsync($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new { name = reference, categoryId = category.GetProperty("id").GetGuid(), amount = 12.345m, paymentMethod = 0, paymentDate = at });
        invalidMoney.IsSuccessStatusCode.ShouldBeFalse();
    }
    [Fact]
    public async Task Approved_refund_projection_is_replaced_by_confirmed_expense_and_linked_return()
    {
        var at = await SeedScopeAsync();
        var refundId = Guid.NewGuid(); var voucher = $"REF-{Guid.NewGuid():N}";
        var category = await Client.PostAsJsonAsync($"/api/expense-categories?academicYearId={Factory.SeededAcademicYearId}", new { name = $"Refund {Guid.NewGuid():N}" });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        Guid studentId = Guid.Empty;
        await WithTenantAsync(async services =>
        {
            var students = services.GetRequiredService<IRepository<Student, Guid>>();
            studentId = await students.AsyncExecuter.FirstAsync((await students.GetQueryableAsync()).Where(x => x.AcademicYearId == Factory.SeededAcademicYearId).Select(x => x.Id));
            // Isolated read-model fixtures model canonical states, not payout command execution.
            var request = (FeeRefundRequest)Activator.CreateInstance(typeof(FeeRefundRequest), nonPublic: true)!;
            typeof(FeeRefundRequest).GetProperty("Id")!.SetValue(request, refundId);
            foreach (var field in new (string Name, object? Value)[] {
                ("TenantId", Guid.Parse(TestAuthHandler.TestTenantId)), ("AcademicYearId", Factory.SeededAcademicYearId),
                ("StudentId", studentId), ("VoucherNumber", voucher), ("Status", FeeRefundStatus.Approved),
                ("ApprovedAmount", 200m), ("TotalAmount", 200m), ("ExpenseCategoryId", categoryId),
                ("ExpenseCategoryName", "Student refunds"), ("RefundMethod", "Cash"), ("CreationTime", at) })
                typeof(FeeRefundRequest).GetProperty(field.Name)!.SetValue(request, field.Value);
            await services.GetRequiredService<IRepository<FeeRefundRequest, Guid>>().InsertAsync(request, autoSave: true);
        });
        var url = $"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction=expense&source=refund&search={voucher}";
        var pending = await ReadPageAsync(url);
        pending.Items.Single().SourceStatus.ShouldBe("Awaiting payout");
        pending.Items.Single().Amount.ShouldBe(200m);
        using var detailTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var studentDetailResponse = await Client.GetAsync(
            $"/api/accounts/register/refund/{refundId}?academicYearId={Factory.SeededAcademicYearId}", detailTimeout.Token);
        studentDetailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await studentDetailResponse.Content.ReadAsStringAsync());
        var studentDetail = await studentDetailResponse.Content.ReadFromJsonAsync<AccountsActivityDetailDto>(JsonOptions);
        pending.Items.Single().GrNumber.ShouldBe(studentDetail!.GrNumber);
        pending.Items.Single().ClassDivision.ShouldBe(studentDetail.ClassName == null && studentDetail.DivisionName == null ? "" :
            $"{studentDetail.ClassName ?? "—"} - {studentDetail.DivisionName ?? "—"}");
        var identityFilter = ColumnFilters(new { column = "studentIdentity", type = "contains", value = pending.Items.Single().StudentName });
        (await ReadPageAsync(url + identityFilter)).TotalCount.ShouldBe(1);
        await WithTenantAsync(async services =>
        {
            var refunds = services.GetRequiredService<IRepository<FeeRefundRequest, Guid>>();
            var request = await refunds.GetAsync(refundId);
            typeof(FeeRefundRequest).GetProperty("Status")!.SetValue(request, FeeRefundStatus.Processed);
            await refunds.UpdateAsync(request, autoSave: true);
            var transactions = services.GetRequiredService<IRepository<Transaction, Guid>>();
            await transactions.InsertAsync(Transaction.Automatic(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
                Factory.SeededAcademicYearId, categoryId, "Student refunds", null, null, voucher, 200m, 0, at,
                TransactionType.Expense, $"refund:{refundId}:paid", refundId, studentId), autoSave: true);
        });
        var paid = await ReadPageAsync(url);
        paid.TotalCount.ShouldBe(1); paid.Items.Single().SourceStatus.ShouldBe("Paid");
        await WithTenantAsync(async services =>
        {
            await services.GetRequiredService<IRepository<Transaction, Guid>>().InsertAsync(Transaction.Automatic(Guid.NewGuid(),
                Guid.Parse(TestAuthHandler.TestTenantId), Factory.SeededAcademicYearId, categoryId, "Student refunds", null, null,
                voucher, 200m, 0, at.AddMinutes(1), TransactionType.Expense, $"refund:{refundId}:returned", refundId, studentId,
                confirmed: true, correction: true), autoSave: true);
        });
        var returned = await ReadPageAsync(url);
        returned.TotalCount.ShouldBe(2); returned.Items.Sum(x => x.Amount).ShouldBe(0m);
        returned.Items.Count(x => x.SourceStatus == "Awaiting payout").ShouldBe(0);
    }
    private static string ColumnFilters(params object[] filters) => "&filters=" +
        Uri.EscapeDataString(System.Text.Json.JsonSerializer.Serialize(filters));

    private async Task<AccountsActivityPageDto> ReadPageAsync(string url)
    {
        var response = await Client.GetAsync(url);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AccountsActivityPageDto>(JsonOptions))!;
    }
    private async Task WithTenantAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
        using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        { await action(services); await uow.CompleteAsync(); }
    }
    private async Task<DateTime> SeedScopeAsync()
    {
        using var scope = Factory.Services.CreateScope(); var services = scope.ServiceProvider;
        using (services.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
        using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var students = services.GetRequiredService<IRepository<Student, Guid>>();
            if (!await students.AnyAsync(x => x.AcademicYearId == Factory.SeededAcademicYearId))
                await students.InsertAsync(new Student(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), "Accounts", null, "Test", Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId), autoSave: true);
            var year = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(Factory.SeededAcademicYearId);
            var at = DateTime.SpecifyKind(year.StartDate.Date.AddHours(6.5), DateTimeKind.Utc);
            await uow.CompleteAsync(); return at;
        }
    }
}
