using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Infrastructure.Auth;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiStatutoryApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/payroll/statutory-schemes/esi/configuration";
	[Fact]
	public async Task Settings_require_authentication_and_view_permission()
	{
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await denied.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.OK);
	}


    private async Task<SaveEsiConfigurationDto> DraftAsync(bool enabled = true) => new()
    {
        Enabled = enabled,
        ConcurrencyStamp = (await GetAndReadAsync<EsiConfigurationSettingsDto>(Url)).Current!.ConcurrencyStamp,
        IdempotencyKey = Guid.NewGuid()
    };

    [Fact]
    public async Task Save_uses_current_month_and_retries_are_idempotent()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var draft = await DraftAsync();
        var response = await Client.PutAsJsonAsync(Url, draft);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var saved = (await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!;
        saved.Current!.Enabled.ShouldBeTrue();
        saved.Current.RegistrationNumber.ShouldBeNull();
        saved.Current.CoveredSince.ShouldBeNull();
        saved.Current.EffectiveMonth.ShouldBe(saved.SuggestedEffectiveMonth);
        var retry = await Client.PutAsJsonAsync(Url, draft);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await retry.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!.Current!.Id.ShouldBe(saved.Current.Id);
        draft.RegistrationNumber = "Changed retry";
        (await Client.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        draft.IdempotencyKey = Guid.NewGuid();
        (await Client.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    internal async Task Concurrent_saves_accept_exactly_one_reviewed_version()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var first = await DraftAsync();
        var second = new SaveEsiConfigurationDto {
            Enabled = false, ConcurrencyStamp = first.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid()
        };
        var responses = await Task.WhenAll(Client.PutAsJsonAsync(Url, first), Client.PutAsJsonAsync(Url, second));
        responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(x => x.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
        settings.History.Count(x => !x.IsCancelled && x.EffectiveMonth == settings.SuggestedEffectiveMonth).ShouldBe(1);
    }

    [Fact]
    public async Task Save_invalidates_current_future_unlocked_but_preserves_locked_and_earlier_snapshots()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
        var month = DateTime.ParseExact(settings.SuggestedEffectiveMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        var earlierId = Guid.NewGuid();
        var lockedId = Guid.NewGuid();
        var unlockedId = Guid.NewGuid();
        var futureId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
        using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
            foreach(var item in new[] { (earlierId, -1, false), (lockedId, 0, true), (unlockedId, 0, false), (futureId, 1, false) })
            {
                var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId),
                    month.AddMonths(item.Item2).ToString("yyyy-MM"), item.Item1);
                if(item.Item3) snapshot.MarkLocked();
                await repo.InsertAsync(snapshot);
                await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>().InsertAsync(
                    new PayrollRunReview(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), snapshot.MonthYear,
                        item.Item1, Guid.NewGuid(), DateTime.UtcNow));
            }
            await uow.CompleteAsync();
        }
        var response = await Client.PutAsJsonAsync(Url, await DraftAsync(false));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var check = Factory.Services.CreateScope();
        using var tenant = check.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
        using var checkUow = check.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        var snapshots = check.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
        var reviews = check.ServiceProvider.GetRequiredService<IRepository<PayrollRunReview, Guid>>();
        foreach(var id in new[] { earlierId, lockedId })
        {
            (await snapshots.AnyAsync(x => x.EmployeeId == id)).ShouldBeTrue();
            (await reviews.AnyAsync(x => x.EmployeeId == id)).ShouldBeTrue();
        }
        foreach(var id in new[] { unlockedId, futureId })
        {
            (await snapshots.AnyAsync(x => x.EmployeeId == id)).ShouldBeFalse();
            (await reviews.AnyAsync(x => x.EmployeeId == id)).ShouldBeFalse();
        }
    }

    internal async Task Save_waits_for_the_payroll_lock_fence_and_preserves_its_result()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var draft = await DraftAsync(false);
        var month = (await GetAndReadAsync<EsiConfigurationSettingsDto>(Url)).SuggestedEffectiveMonth;
        var employeeId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true);
        await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
        var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), month, employeeId);
        snapshot.MarkLocked();
        await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot, autoSave: true);
        var save = Client.PutAsJsonAsync(Url, draft);
        await Task.Delay(200);
        save.IsCompleted.ShouldBeFalse();
        await uow.CompleteAsync();
        var response = await save.WaitAsync(TimeSpan.FromSeconds(15));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var check = Factory.Services.CreateScope();
        using var checkTenant = check.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
        using var checkUnit = check.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
        (await check.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetAsync(snapshot.Id)).IsLocked.ShouldBeTrue();
    }

    [Fact]
    public async Task Same_month_edits_keep_audit_history()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        var original = await DraftAsync();
        var first = await Client.PutAsJsonAsync(Url, original);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstId = (await first.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!.Current!.Id;
        var edited = await DraftAsync(false);
        edited.RegistrationNumber = "TEST-REGISTRATION";
        var response = await Client.PutAsJsonAsync(Url, edited);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = (await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!;
        settings.Current!.RegistrationNumber.ShouldBe("TEST-REGISTRATION");
        settings.History.Single(x => x.Id == firstId).IsCancelled.ShouldBeTrue();
    }

    [Fact]
    public async Task Save_requires_idempotency_and_concurrency_tokens()
    {
        await EnsureDefaultTestUserHasTenantWideScopeAsync();
        (await Client.PutAsJsonAsync(Url, new SaveEsiConfigurationDto())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
	[Fact]
	public async Task Rules_remain_read_only_and_versioned()
	{
		var rule = await GetAndReadAsync<StatutoryRuleVersionDto>("/api/payroll/statutory-schemes/esi/rule?effectiveOn=2026-09-01");
		rule.EmployeePercentage.ShouldBe(0.75m);
		rule.EmployerPercentage.ShouldBe(3.25m);
		rule.EmployeeShareDailyWageExemption.ShouldBe(176m);
	}
}
