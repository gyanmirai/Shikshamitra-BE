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
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Identity;
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
        // Generic register filtering is independent of the separately tested cash
        // approval/funding workflow. Use an actual non-cash bank destination here.
        var bankId = Guid.NewGuid();
        await WithTenantAsync(async services => await services.GetRequiredService<IRepository<ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount, Guid>>()
            .InsertAsync(new ShikshaMitra.Domain.Finance.Accounts.Settings.BankAccount(bankId, Factory.SeededAcademicYearId,
                "Register test bank", "Synthetic branch", null, "School", Enum.GetValues<ShikshaMitra.Finance.Accounts.Settings.BankAccountType>().First(),
                BitConverter.ToUInt64(bankId.ToByteArray(), 0).ToString(), "Register testing", "TEST0001234", null,
                tenantId: Guid.Parse(TestAuthHandler.TestTenantId))));
        var body = new { idempotencyKey = requestKey, name = reference, counterparty = "Parent association", categoryId = category.GetProperty("id").GetGuid(), amount = 12.34m, paymentMethod = 3, bankAccountId = bankId, paymentDate = at, paymentReference = "REF-123", categoryTags = Array.Empty<string>() };
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
            amount = 5m, paymentMethod = 3, bankAccountId = bankId, paymentDate = at.AddMinutes(1), categoryTags = Array.Empty<string>() });
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
    [Theory]
    [InlineData(FeeRefundStatus.Approved)]
    [InlineData(FeeRefundStatus.Processing)]
    public async Task Unpaid_refunds_are_excluded_until_confirmed_expense_and_linked_return(FeeRefundStatus unpaidStatus)
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
                ("StudentId", studentId), ("VoucherNumber", voucher), ("Status", unpaidStatus),
                ("ApprovedAmount", 200m), ("TotalAmount", 200m), ("ExpenseCategoryId", categoryId),
                ("ExpenseCategoryName", "Student refunds"), ("RefundMethod", "Cash"), ("CreationTime", at) })
                typeof(FeeRefundRequest).GetProperty(field.Name)!.SetValue(request, field.Value);
            await services.GetRequiredService<IRepository<FeeRefundRequest, Guid>>().InsertAsync(request, autoSave: true);
        });
        var url = $"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction=expense&source=refund&search={voucher}";
        var pending = await ReadPageAsync(url);
        pending.TotalCount.ShouldBe(0);
        pending.Items.ShouldBeEmpty();
        using var detailTimeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var studentDetailResponse = await Client.GetAsync(
            $"/api/accounts/register/refund/{refundId}?academicYearId={Factory.SeededAcademicYearId}", detailTimeout.Token);
        studentDetailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await studentDetailResponse.Content.ReadAsStringAsync());
        var studentDetail = await studentDetailResponse.Content.ReadFromJsonAsync<AccountsActivityDetailDto>(JsonOptions);
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
        paid.Items.Single().GrNumber.ShouldBe(studentDetail!.GrNumber);
        paid.Items.Single().ClassDivision.ShouldBe(studentDetail.ClassName == null && studentDetail.DivisionName == null ? "" :
            $"{studentDetail.ClassName ?? "—"} - {studentDetail.DivisionName ?? "—"}");
        var identityFilter = ColumnFilters(new { column = "studentIdentity", type = "contains", value = paid.Items.Single().StudentName });
        (await ReadPageAsync(url + identityFilter)).TotalCount.ShouldBe(1);
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Applicant_collection_remains_visible_before_and_after_student_link(bool linked)
    {
        var at = await SeedScopeAsync();
        var accountId = Guid.NewGuid(); var collectionId = Guid.NewGuid(); var receiptId = Guid.NewGuid();
        var reference = $"M-{Guid.NewGuid():N}"[..20]; var applicant = $"Applicant {Guid.NewGuid():N}";
        await WithTenantAsync(async services =>
        {
            Guid? studentId = linked ? await services.GetRequiredService<IRepository<Student, Guid>>().AsyncExecuter.FirstAsync(
                (await services.GetRequiredService<IRepository<Student, Guid>>().GetQueryableAsync())
                    .Where(x => x.AcademicYearId == Factory.SeededAcademicYearId).Select(x => x.Id)) : null;
            await services.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(new FinanceFeeAccount(
                accountId, Guid.Parse(TestAuthHandler.TestTenantId), Guid.NewGuid(), Guid.NewGuid(), Factory.SeededAcademicYearId,
                Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, applicant,
                studentId: studentId), autoSave: true);
            await services.GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(new FeeRecordMetadata(
                collectionId, Guid.Parse(TestAuthHandler.TestTenantId), studentId, Factory.SeededAcademicYearId, 42,
                reference, FeeCollectionStatus.Paid, financeFeeAccountId: accountId), autoSave: true);
            var receipt = new FeePaymentReceipt(receiptId)
            {
                TenantId = Guid.Parse(TestAuthHandler.TestTenantId), FeeCollectionTransactionId = collectionId,
                StudentId = studentId, AcademicYearId = Factory.SeededAcademicYearId, ReceiptNumber = reference,
                ReceiptKind = FeeReceiptKind.FinalFee, TotalPaidAmount = 42, CreationTime = at
            };
            receipt.SetFinanceFeeAccount(accountId);
            await services.GetRequiredService<IRepository<FeePaymentReceipt, Guid>>().InsertAsync(receipt, autoSave: true);
        });
        var url = $"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction=income&source=collection&search={reference}";
        var page = await ReadPageAsync(url);
        page.TotalCount.ShouldBe(1); page.Items.Single().FinanceFeeAccountId.ShouldBe(accountId);
        page.Items.Single().StudentName.ShouldBe(applicant); page.Items.Single().Amount.ShouldBe(42m);
        (await ReadPageAsync(url + ColumnFilters(new { column = "studentIdentity", type = "contains", value = applicant })))
            .TotalCount.ShouldBe(1);
        var legacy = await ReadPageAsync($"/api/accounts/activity?academicYearId={Factory.SeededAcademicYearId}&source=collection&search={reference}");
        legacy.Items.Single().FinanceFeeAccountId.ShouldBe(accountId);
        var detailResponse = await Client.GetAsync($"/api/accounts/register/collection/{receiptId}?academicYearId={Factory.SeededAcademicYearId}");
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
        var detail = await detailResponse.Content.ReadFromJsonAsync<AccountsActivityDetailDto>(JsonOptions);
        detail!.FinanceFeeAccountId.ShouldBe(accountId); detail.StudentName.ShouldBe(applicant);
        var categoryResponse = await Client.PostAsJsonAsync($"/api/income-categories?academicYearId={Factory.SeededAcademicYearId}",
            new { name = $"Merge {Guid.NewGuid():N}" });
        categoryResponse.EnsureSuccessStatusCode();
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetGuid();
        var automaticId = Guid.NewGuid();
        await WithTenantAsync(async services =>
        {
            await services.GetRequiredService<IRepository<Transaction, Guid>>().InsertAsync(Transaction.Automatic(
                automaticId, Guid.Parse(TestAuthHandler.TestTenantId), Factory.SeededAcademicYearId, categoryId, "Merge collection",
                null, null, reference, 42m, 0, at, TransactionType.Income, $"collection:{collectionId}", collectionId,
                page.Items.Single().StudentId, financeFeeAccountId: accountId), autoSave: true);
        });
        var confirmed = await ReadPageAsync(url);
        confirmed.TotalCount.ShouldBe(1); confirmed.Items.Single().Id.ShouldBe(automaticId);
        confirmed.Items.Single().StudentName.ShouldBe(applicant);
        (await ReadPageAsync($"/api/accounts/activity?academicYearId={Factory.SeededAcademicYearId}&source=collection&search={reference}"))
            .TotalCount.ShouldBe(1);
        var automaticDetail = await Client.GetAsync($"/api/accounts/register/collection/{automaticId}?academicYearId={Factory.SeededAcademicYearId}");
        automaticDetail.StatusCode.ShouldBe(HttpStatusCode.OK, await automaticDetail.Content.ReadAsStringAsync());
        (await automaticDetail.Content.ReadFromJsonAsync<AccountsActivityDetailDto>(JsonOptions))!.FinanceFeeAccountId.ShouldBe(accountId);
        using var restricted = await DivisionScopedClientAsync();
        var deniedPage = await restricted.GetFromJsonAsync<AccountsActivityPageDto>(url, JsonOptions);
        deniedPage!.TotalCount.ShouldBe(0);
        (await restricted.GetAsync($"/api/accounts/register/collection/{receiptId}?academicYearId={Factory.SeededAcademicYearId}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await restricted.GetAsync($"/api/accounts/register/collection/{automaticId}?academicYearId={Factory.SeededAcademicYearId}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Applicant_refund_is_visible_only_when_paid_and_keeps_payer_identity(bool linked)
    {
        var at = await SeedScopeAsync(); var refundId = Guid.NewGuid(); var accountId = Guid.NewGuid();
        var voucher = $"M-{Guid.NewGuid():N}"; var applicant = $"Refund applicant {Guid.NewGuid():N}";
        await WithTenantAsync(async services =>
        {
            var students = services.GetRequiredService<IRepository<Student, Guid>>();
            Guid? studentId = linked ? await students.AsyncExecuter.FirstAsync((await students.GetQueryableAsync())
                .Where(x => x.AcademicYearId == Factory.SeededAcademicYearId).Select(x => x.Id)) : null;
            await services.GetRequiredService<IRepository<FinanceFeeAccount, Guid>>().InsertAsync(new FinanceFeeAccount(
                accountId, Guid.Parse(TestAuthHandler.TestTenantId), Guid.NewGuid(), Guid.NewGuid(), Factory.SeededAcademicYearId,
                Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, applicant,
                studentId: studentId), autoSave: true);
            // Canonical read-model fixtures; no payout commands are executed here.
            var refund = (FeeRefundRequest)Activator.CreateInstance(typeof(FeeRefundRequest), nonPublic: true)!;
            foreach (var field in new (string Name, object? Value)[] {
                ("Id", refundId), ("TenantId", Guid.Parse(TestAuthHandler.TestTenantId)), ("AcademicYearId", Factory.SeededAcademicYearId),
                ("StudentId", studentId), ("FinanceFeeAccountId", accountId), ("VoucherNumber", voucher),
                ("Status", FeeRefundStatus.Approved), ("ApprovedAmount", 25m), ("TotalAmount", 25m),
                ("RefundMethod", "Cash"), ("CreationTime", at) })
                typeof(FeeRefundRequest).GetProperty(field.Name)!.SetValue(refund, field.Value);
            await services.GetRequiredService<IRepository<FeeRefundRequest, Guid>>().InsertAsync(refund, autoSave: true);
        });
        var url = $"/api/accounts/register?academicYearId={Factory.SeededAcademicYearId}&direction=expense&source=refund&search={voucher}";
        var pending = await ReadPageAsync(url);
        pending.TotalCount.ShouldBe(0);
        pending.Items.ShouldBeEmpty();
        var detailResponse = await Client.GetAsync($"/api/accounts/register/refund/{refundId}?academicYearId={Factory.SeededAcademicYearId}");
        detailResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await detailResponse.Content.ReadAsStringAsync());
        (await detailResponse.Content.ReadFromJsonAsync<AccountsActivityDetailDto>(JsonOptions))!.FinanceFeeAccountId.ShouldBe(accountId);
        using var restricted = await DivisionScopedClientAsync();
        (await restricted.GetFromJsonAsync<AccountsActivityPageDto>(url, JsonOptions))!.TotalCount.ShouldBe(0);
        (await restricted.GetAsync($"/api/accounts/register/refund/{refundId}?academicYearId={Factory.SeededAcademicYearId}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await WithTenantAsync(async services =>
        {
            var refunds = services.GetRequiredService<IRepository<FeeRefundRequest, Guid>>();
            var refund = await refunds.GetAsync(refundId);
            typeof(FeeRefundRequest).GetProperty("Status")!.SetValue(refund, FeeRefundStatus.Processed);
            await refunds.UpdateAsync(refund, autoSave: true);
            var entry = new FeeRefundAccountingEntry(
                Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), refundId, Guid.NewGuid(), null, null, "Cash", null,
                25m, false, null, "[]", "Synthetic merge regression", Guid.Parse(TestAuthHandler.TestUserId));
            typeof(FeeRefundAccountingEntry).GetProperty("PostedAt")!.SetValue(entry, new DateTimeOffset(at));
            await services.GetRequiredService<IRepository<FeeRefundAccountingEntry, Guid>>().InsertAsync(entry, autoSave: true);
        });
        var paid = await ReadPageAsync(url);
        paid.TotalCount.ShouldBe(1); paid.Items.Single().Amount.ShouldBe(25m);
        paid.Items.Single().SourceStatus.ShouldBe("Paid");
        paid.Items.Single().StudentName.ShouldBe(applicant); paid.Items.Single().FinanceFeeAccountId.ShouldBe(accountId);
        (await restricted.GetFromJsonAsync<AccountsActivityPageDto>(url, JsonOptions))!.TotalCount.ShouldBe(0);
    }

    private async Task<System.Net.Http.HttpClient> DivisionScopedClientAsync()
    {
        var id = Guid.NewGuid();
        await WithTenantAsync(async services =>
        {
            var tenant = Guid.Parse(TestAuthHandler.TestTenantId);
            (await services.GetRequiredService<IdentityUserManager>().CreateAsync(
                new IdentityUser(id, $"merge-{id:N}", $"merge-{id:N}@example.test", tenant))).Succeeded.ShouldBeTrue();
            await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
                Guid.NewGuid(), tenant, userId: id, academicYearId: Factory.SeededAcademicYearId, educationBoardId: Factory.SeededBoardId,
                educationMediumId: Factory.SeededMediumId, educationMediumStageId: Factory.SeededMediumStageId,
                standardId: Factory.SeededStandardId, divisionId: Factory.SeededDivisionId), autoSave: true);
        });
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, id.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
        return client;
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
