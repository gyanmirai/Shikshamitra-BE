using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Timing;
using Xunit;
namespace ShikshaMitra.Finance.Accounts;
public partial class AccountSettingsQaRegressionApi_Tests
{
    [Fact]
    public async Task Complete_history_returns_newest_and_deep_pages_beyond_one_thousand_changes()
    {
        await EnsureAcademicScopeAsync();
        var entity = await PostAndReadAsync<JsonElement>($"/api/income-categories?academicYearId={Factory.SeededAcademicYearId}", new { name = $"History {Guid.NewGuid():N}"[..39], tags = Array.Empty<string>() });
        var id = entity.GetProperty("id").GetGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            using var tenant = provider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
            using var uow = provider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
            var repository = provider.GetRequiredService<IAuditLogRepository>();
            // Use the same clock as persisted entity auditing: the fixture can run in a non-UTC timezone.
            var auditTime = provider.GetRequiredService<IClock>().Now;
            var info = new AuditLogInfo { TenantId = Guid.Parse(TestAuthHandler.TestTenantId), ExecutionTime = auditTime };
            for (var index = 1; index <= 1005; index++)
                info.EntityChanges.Add(new EntityChangeInfo {
                    EntityId = id.ToString(), EntityTypeFullName = typeof(TransactionCategory).FullName!,
                    ChangeType = EntityChangeType.Updated, ChangeTime = auditTime.AddMinutes(index),
                    PropertyChanges = new List<EntityPropertyChangeInfo> {
                        new() { PropertyName = "Name", PropertyTypeFullName = typeof(string).FullName!, NewValue = $"Change {index}" },
                        new() { PropertyName = "AccountNumber", PropertyTypeFullName = typeof(string).FullName!, NewValue = "SENSITIVE" }
                    }
                });
            await repository.InsertAsync(await provider.GetRequiredService<IAuditLogInfoToAuditLogConverter>().ConvertAsync(info), autoSave: true);
            await uow.CompleteAsync();
        }
        var newest = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}/versions?skipCount=0&maxResultCount=3");
        newest.GetProperty("totalCount").GetInt64().ShouldBe(1006);
        newest.GetProperty("items")[0].GetProperty("versionNumber").GetInt32().ShouldBe(1006);
        newest.GetProperty("items")[0].ToString().ShouldContain("Change 1005");
        newest.ToString().ShouldNotContain("SENSITIVE");
        var oldest = await GetAndReadAsync<JsonElement>($"/api/income-categories/{id}/versions?skipCount=1004&maxResultCount=3");
        oldest.GetProperty("items").GetArrayLength().ShouldBe(2);
        oldest.GetProperty("items")[1].GetProperty("changeAction").GetString().ShouldBe("created");
        oldest.GetProperty("items")[1].GetProperty("versionNumber").GetInt32().ShouldBe(1);
    }

    [Theory]
    [InlineData("income-categories")]
    [InlineData("bank-accounts")]
    public async Task List_dependency_queries_are_bounded_by_page_not_row_count(string route)
    {
        await EnsureAcademicScopeAsync();
        var prefix = "Batch " + Guid.NewGuid().ToString("N")[..12];
        for (var index = 0; index < 6; index++)
        {
            if (route == "income-categories")
                await PostAndReadAsync<JsonElement>($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new { name = prefix + index, tags = Array.Empty<string>() });
            else await PostAndReadAsync<JsonElement>($"/api/{route}?academicYearId={Factory.SeededAcademicYearId}", new {
                bankName = prefix + index, branchName = "Branch", accountHolderName = "School", accountType = 0,
                accountNumber = Random.Shared.NextInt64(100000000000, 999999999999).ToString(), ifscCode = "QAAA0123456"
            });
        }
        var url = $"/api/{route}?academicYearId={Factory.SeededAcademicYearId}&search={Uri.EscapeDataString(prefix)}";
        int small;
        using (var counter = new ReadCommandCounter())
        {
            (await GetAndReadAsync<JsonElement>(url + "&pageSize=1")).GetProperty("items").GetArrayLength().ShouldBe(1);
            small = counter.Count;
        }
        using (var counter = new ReadCommandCounter())
        {
            var result = await GetAndReadAsync<JsonElement>(url + "&pageSize=6");
            result.GetProperty("items").GetArrayLength().ShouldBe(6);
            result.GetProperty("items").EnumerateArray().ShouldAllBe(x => !x.GetProperty("isUsedInDownstream").GetBoolean());
            counter.Count.ShouldBeLessThanOrEqualTo(small + 2, "Increasing a settings page must not issue per-row dependency reads.");
        }
    }

    private sealed class ReadCommandCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> subscriptions = [];
        private readonly IDisposable all;
        private int count;
        public int Count => count;
        public ReadCommandCounter() => all = DiagnosticListener.AllListeners.Subscribe(this);
        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.EntityFrameworkCore") subscriptions.Add(listener.Subscribe(this));
        }
        public void OnNext(KeyValuePair<string, object?> item)
        {
            if (item.Key == "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted") Interlocked.Increment(ref count);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose() { all.Dispose(); foreach (var subscription in subscriptions) subscription.Dispose(); }
    }
}
