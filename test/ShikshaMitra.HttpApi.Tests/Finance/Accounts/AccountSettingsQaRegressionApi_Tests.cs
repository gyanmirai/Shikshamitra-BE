using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Auditing;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

[CollectionDefinition(Name)]
public class AccountSettingsQaApiCollection : ICollectionFixture<AccountSettingsQaApiFactory>
{
    public const string Name = "Account Settings QA Regression";
}

public class AccountSettingsQaApiFactory : ShikshaMitraApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { [ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal }));
        // The default API fixture disables auditing. This suite verifies actual audit persistence.
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<AbpAuditingOptions>(options => options.IsEnabled = true);
            // The shared SQLite fixture uses one connection. ABP persists audit logs in
            // a separate UoW before the request UoW exits; SQLite cannot nest transactions.
            // Keep real EF/audit persistence while disabling only this fixture's transactions.
            services.PostConfigure<AbpUnitOfWorkDefaultOptions>(options =>
                options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled);
        });
    }
}

[Collection(AccountSettingsQaApiCollection.Name)]
public class AccountSettingsQaRegressionApi_Tests(AccountSettingsQaApiFactory factory) : ShikshaMitraApiTestBase(factory)
{
    [Theory]
    [InlineData("income-categories")]
    [InlineData("expense-categories")]
    public async Task Category_history_records_edits_and_status_changes(string route)
    {
        await EnsureAcademicScopeAsync();
        var created = await PostAndReadAsync<JsonElement>($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new { name = $"QA {Guid.NewGuid():N}", tags = Array.Empty<string>() });
        var id = created.GetProperty("id").GetGuid();
        var name = $"Edit {Guid.NewGuid():N}";
        var update = await Client.PutAsJsonAsync($"/api/{route}/{id}", new { name, description = "शुल्क", tags = Array.Empty<string>(), concurrencyStamp = created.GetProperty("concurrencyStamp").GetString() });
        update.EnsureSuccessStatusCode();
        var edited = await update.Content.ReadFromJsonAsync<JsonElement>();
        var status = await Client.PatchAsJsonAsync($"/api/{route}/{id}/status", new { isActive = false, deactivationReason = "QA regression", concurrencyStamp = edited.GetProperty("concurrencyStamp").GetString() });
        status.EnsureSuccessStatusCode();
        var history = await GetAndReadAsync<JsonElement>($"/api/{route}/{id}/versions");
        var items = history.GetProperty("items").EnumerateArray().ToArray();
        items.Length.ShouldBe(3);
        items.SelectMany(item => item.GetProperty("changedFields").EnumerateArray())
            .Any(field => field.GetProperty("fieldName").GetString() == "Name" && field.GetProperty("newValue").GetString()!.Contains(name)).ShouldBeTrue();
        items.SelectMany(item => item.GetProperty("changedFields").EnumerateArray())
            .Any(field => field.GetProperty("fieldName").GetString() == "IsActive").ShouldBeTrue();
    }

    [Fact]
    public async Task Bank_list_retains_masked_UPI_and_history_never_exposes_payment_identifiers()
    {
        await EnsureAcademicScopeAsync();
        var account = Random.Shared.NextInt64(100000000000, 999999999999).ToString();
        var name = $"QA Bank {Guid.NewGuid():N}";
        var created = await PostAndReadAsync<JsonElement>($"/api/bank-accounts?academicYearId={Factory.SeededAcademicYearId}", new { bankName = name, branchName = "Branch 10 बैंक", accountHolderName = "School 10 & Co", accountType = 0, accountNumber = account, ifscCode = "QAAA0123456", upiId = "private.bank@qa", effectiveFrom = "2026-10-14T00:00:00Z" });
        var id = created.GetProperty("id").GetGuid();
        var list = await GetAndReadAsync<JsonElement>($"/api/bank-accounts?academicYearId={Factory.SeededAcademicYearId}&search={Uri.EscapeDataString(name)}");
        var row = list.GetProperty("items").EnumerateArray().Single();
        row.GetProperty("upiId").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("maskedUpiId").GetString().ShouldBe("***@qa");
        row.GetProperty("accountNumber").ValueKind.ShouldBe(JsonValueKind.Null);
        var update = await Client.PutAsJsonAsync($"/api/bank-accounts/{id}", new { bankName = name, branchName = "Updated Branch", accountHolderName = "School 10 & Co", accountType = 1, accountNumber = account, ifscCode = "QAAA0123456", upiId = "private.bank@qa", purpose = "शुल्क", effectiveFrom = "2026-10-14T00:00:00Z", concurrencyStamp = created.GetProperty("concurrencyStamp").GetString() });
        update.EnsureSuccessStatusCode();
        var edited = await update.Content.ReadFromJsonAsync<JsonElement>();
        var status = await Client.PatchAsJsonAsync($"/api/bank-accounts/{id}/status", new { isActive = false, deactivationReason = "QA regression", concurrencyStamp = edited.GetProperty("concurrencyStamp").GetString() });
        status.EnsureSuccessStatusCode();
        var history = await GetAndReadAsync<JsonElement>($"/api/bank-accounts/{id}/versions");
        history.GetProperty("items").GetArrayLength().ShouldBe(3);
        history.ToString().ShouldContain("Updated Branch");
        history.ToString().ShouldContain("IsActive");
        history.ToString().ShouldNotContain(account);
        history.ToString().ShouldNotContain("private.bank@qa");
        history.ToString().ShouldNotContain("QAAA0123456");
    }

    [Fact]
    public async Task Income_API_rejects_names_beyond_the_displayed_forty_character_limit()
    {
        await EnsureAcademicScopeAsync();
        var response = await Client.PostAsJsonAsync($"/api/income-categories?academicYearId={Factory.SeededAcademicYearId}", new { name = new string('A', 41), tags = Array.Empty<string>() });
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("bankName", 101)]
    [InlineData("branchName", 101)]
    [InlineData("accountHolderName", 101)]
    [InlineData("purpose", 501)]
    public async Task Bank_API_rejects_over_limit_payloads(string field, int length)
    {
        await EnsureAcademicScopeAsync();
        var body = new Dictionary<string, object> { ["bankName"] = "Bank", ["branchName"] = "Branch", ["accountHolderName"] = "Holder", ["accountType"] = 0, ["accountNumber"] = "000001", ["ifscCode"] = "QAAA0123456", [field] = new string('A', length) };
        var response = await Client.PostAsJsonAsync($"/api/bank-accounts?academicYearId={Factory.SeededAcademicYearId}", body);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task EnsureAcademicScopeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
        using (provider.GetRequiredService<ICurrentTenant>().Change(tenantId))
        using (var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var students = provider.GetRequiredService<IRepository<Student, Guid>>();
            if (!await students.AnyAsync(x => x.AcademicYearId == Factory.SeededAcademicYearId))
                await students.InsertAsync(new Student(Guid.NewGuid(), tenantId, "Accounts", null, "QA", Factory.SeededAcademicYearId, Factory.SeededBoardId, Factory.SeededMediumId, Factory.SeededMediumStageId, Factory.SeededStandardId, Factory.SeededDivisionId), autoSave: true);
            await uow.CompleteAsync();
        }
    }
}
