using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public class EsiSetupTestClock : IClock
{
	public DateTime Now { get; set; } = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
	public DateTimeKind Kind => DateTimeKind.Utc;
	public bool SupportsMultipleTimezone => true;
	public DateTime Normalize(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public class EsiHistoricalTestFactory : ShikshaMitraApiTestFactory
{
	public EsiSetupTestClock TestClock { get; } = new();
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IClock>(TestClock)));
	}
}

// The fixture owns an isolated synthetic database; every case calls the real HTTP save endpoint.
[Collection(ApiTestCollection.Name)]
public class EsiHistoricalConfigurationApi_Tests : ShikshaMitraApiTestBase, IClassFixture<EsiHistoricalTestFactory>, IDisposable
{
	public EsiHistoricalConfigurationApi_Tests(EsiHistoricalTestFactory factory) : this((ShikshaMitraApiTestFactory)factory) { }
	internal EsiHistoricalConfigurationApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }
	private const string Url = "/api/payroll/statutory-schemes/esi/configuration";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	public void Dispose() => Client.Dispose();

	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await action(scope.ServiceProvider);
		await unit.CompleteAsync();
	}

	private async Task InitializeAsync()
	{
		if (Factory is EsiHistoricalTestFactory fixedFactory)
			fixedFactory.TestClock.Now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await InTenantAsync(async services => {
			var configurations = services.GetRequiredService<IRepository<EsiPayrollConfiguration, Guid>>();
			foreach (var configuration in await configurations.GetListAsync())
				await configurations.HardDeleteAsync(configuration);
			await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().DeleteAsync(x => true);
			await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().DeleteAsync(x => true);
			await services.GetRequiredService<IRepository<SalarySlip, Guid>>().DeleteAsync(x => true);
		});
	}

	private async Task<SaveEsiConfigurationDto> DraftAsync(string month)
	{
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
		var selected = settings.History.Where(x => !x.IsCancelled && string.Compare(x.EffectiveMonth, month, StringComparison.Ordinal) <= 0)
			.OrderByDescending(x => x.EffectiveMonth).First();
		return new() { EffectiveMonth = month, Enabled = true, ConcurrencyStamp = selected.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid(), RegistrationNumber = "SYNTHETIC" };
	}

	[Fact]
	public async Task June_initial_setup_and_unlocked_updates_preserve_order_history_and_retries()
	{
		await InitializeAsync();
		var future = await DraftAsync("2026-12");
		(await Client.PutAsJsonAsync(Url, future)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var draft = await DraftAsync("2026-06");
		var componentResponse = await Client.PostAsJsonAsync("/api/payroll/earning-components",
			new ShikshaMitra.HR.Payroll.Settings.EarningComponentCreateRequestDto {
				EarningComponentName = "Historical synthetic earning " + Guid.NewGuid().ToString("N"),
				EarningType = "FIXED", CalculationType = "AMOUNT", Amount = 1000m, VisibleInPayslip = true
			}, JsonOptions);
		componentResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
		var component = (await componentResponse.Content.ReadFromJsonAsync<ShikshaMitra.HR.Payroll.Settings.ComponentCreateResponseDto>(JsonOptions))!;
		draft.Treatments = [new EsiTreatmentDraftDto { ComponentId = component.Id, Treatment = "Included" }];
		var response = await Client.PutAsJsonAsync(Url, draft);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!;
		saved.Current!.EffectiveMonth.ShouldBe("2026-06");
		saved.History.Count(x => !x.IsCancelled && x.EffectiveMonth == "2026-12").ShouldBe(1);
		(await Client.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.OK);
		draft.RegistrationNumber = "DIFFERENT";
		(await Client.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		draft.IdempotencyKey = Guid.NewGuid();
		(await Client.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var update = await DraftAsync("2026-06");
		update.RegistrationNumber = "UPDATED";
		(await Client.PutAsJsonAsync(Url, update)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var latest = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
		latest.History.Single(x => x.Id == saved.Current.Id).IsCancelled.ShouldBeTrue();
		latest.Current!.RegistrationNumber.ShouldBe("UPDATED");
		var classifications = await GetAndReadAsync<System.Collections.Generic.List<PayrollComponentStatutoryTreatmentDto>>(
			"/api/payroll/statutory-schemes/esi/component-treatments?effectiveOn=2026-06-01");
		classifications.Single(x => x.ComponentId == component.Id).Treatment.ShouldBe("Included");
	}

	[Theory]
	[InlineData("locked", "2026-06")]
	[InlineData("locked", "2026-12")]
	[InlineData("finalized", "2026-06")]
	[InlineData("paid", "2026-07")]
	public async Task Historical_setup_cannot_change_protected_payroll_or_later_continuity(string protection, string month)
	{
		await InitializeAsync();
		if (protection == "finalized")
			(await Client.PutAsJsonAsync(Url, await DraftAsync("2026-06"))).StatusCode.ShouldBe(HttpStatusCode.OK);
		var draft = await DraftAsync("2026-06");
		await InTenantAsync(async services => {
			var employee = Guid.NewGuid();
			if (protection == "locked") {
				var snapshot = new PayrollRunItemSnapshot(Guid.NewGuid(), TenantId, month, employee) {
					GrossSalary = 20000m, FullGrossSalary = 20000m, NetPay = 19650m, TotalDeduction = 350m,
					EmployerContributionTotal = 650m, EmployerCost = 20650m,
					EsiConfigurationId = Guid.NewGuid(), StatutoryApplicationJson = "{\"synthetic\":true}",
					EsiCalculationDetailsJson = "{\"wages\":20000}",
					EarningComponentsJson = "[{\"name\":\"Basic\",\"amount\":20000}]"
				};
				snapshot.MarkLocked();
				await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(snapshot);
			} else {
				var slip = (SalarySlip)Activator.CreateInstance(typeof(SalarySlip), BindingFlags.Instance | BindingFlags.NonPublic,
					null, new object?[] { Guid.NewGuid(), TenantId, employee, month,
						new List<SalarySlipLineItem> { new("Basic", 20000m) },
						new List<SalarySlipLineItem> { new("ESIC", 150m), new("Professional tax", 200m) }, DateTime.UtcNow, null }, null)!;
				slip.FinalizeSlip();
				if (protection == "paid") slip.RecordPayment(DateTimeOffset.UtcNow, "SYNTHETIC", Guid.NewGuid(), Guid.NewGuid());
				await services.GetRequiredService<IRepository<SalarySlip, Guid>>().InsertAsync(slip);
			}
		});
		var before = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
		var payrollBefore = await StoredPayrollAsync();
		var response = await Client.PutAsJsonAsync(Url, draft);
		response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EsiConfigurationLocked);
		(await GetAndReadAsync<EsiConfigurationSettingsDto>(Url)).History.Count.ShouldBe(before.History.Count);
		(await StoredPayrollAsync()).ShouldBe(payrollBefore);
	}

	[Fact]
	public async Task Historical_save_invalidates_unlocked_reviews_but_keeps_earlier_periods()
	{
		await InitializeAsync();
		var employee = Guid.NewGuid();
		await InTenantAsync(async services => {
			foreach (var month in new[] { "2026-05", "2026-06", "2026-07" }) {
				await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(
					new PayrollRunItemSnapshot(Guid.NewGuid(), TenantId, month, employee));
				await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().InsertAsync(
					new PayrollRunReview(Guid.NewGuid(), TenantId, month, employee, Guid.NewGuid(), DateTime.UtcNow));
			}
		});
		(await Client.PutAsJsonAsync(Url, await DraftAsync("2026-06"))).StatusCode.ShouldBe(HttpStatusCode.OK);
		await InTenantAsync(async services => {
			(await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetListAsync(x => x.EmployeeId == employee))
				.Select(x => x.MonthYear).ShouldBe(new[] { "2026-05" });
			(await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().GetListAsync(x => x.EmployeeId == employee))
				.Select(x => x.MonthYear).ShouldBe(new[] { "2026-05" });
		});
	}

	[Fact]
	public async Task Indian_month_rollover_keeps_a_draft_saveable_and_permissions_enforced()
	{
		await InitializeAsync();
		var clock = ((EsiHistoricalTestFactory)Factory).TestClock;
		clock.Now = new DateTime(2026, 10, 31, 18, 29, 0, DateTimeKind.Utc);
		var draft = await DraftAsync("2026-10");
		clock.Now = clock.Now.AddMinutes(2); // November in India, still October in UTC.
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await denied.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var response = await Client.PutAsJsonAsync(Url, draft);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		(await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!.SuggestedEffectiveMonth.ShouldBe("2026-12");
	}

	internal async Task Historical_save_waits_for_finalization_and_rolls_back_failed_invalidation()
	{
		await InitializeAsync();
		var draft = await DraftAsync("2026-06");
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true)) {
			await scope.ServiceProvider.GetRequiredService<IPayrollInputRevisionStore>().LockAsync();
			var locked = new PayrollRunItemSnapshot(Guid.NewGuid(), TenantId, "2026-06", Guid.NewGuid());
			locked.MarkLocked();
			await scope.ServiceProvider.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(locked, autoSave: true);
			var request = Client.PutAsJsonAsync(Url, draft);
			await Task.Delay(200);
			request.IsCompleted.ShouldBeFalse();
			await unit.CompleteAsync();
			(await request.WaitAsync(TimeSpan.FromSeconds(20))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		}
		await InitializeAsync();
		// Inject failure after same-month cancellation/new-version insertion, during invalidation.
		(await Client.PutAsJsonAsync(Url, await DraftAsync("2026-06"))).StatusCode.ShouldBe(HttpStatusCode.OK);
		draft = await DraftAsync("2026-06");
		var before = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
		var rollbackEmployee = Guid.NewGuid();
		await InTenantAsync(async services => {
			var employee = rollbackEmployee;
			await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().InsertAsync(
				new PayrollRunItemSnapshot(Guid.NewGuid(), TenantId, "2026-06", employee) {
					GrossSalary = 20000m, NetPay = 19650m, TotalDeduction = 350m,
					StatutoryApplicationJson = "{\"syntheticRollbackEvidence\":true}"
				});
			await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().InsertAsync(
				new PayrollRunReview(Guid.NewGuid(), TenantId, "2026-06", employee, Guid.NewGuid(), DateTime.UtcNow));
		});
		var payrollBefore = await StoredPayrollAsync();
		var failure = ((EsiHistoricalPostgresFactory)Factory).CommitFailure;
		failure.Arm(rollbackEmployee);
		// ABP commits after response generation; the injected commit failure aborts the response stream.
		var aborted = await Should.ThrowAsync<System.Net.Http.HttpRequestException>(() => Client.PutAsJsonAsync(Url, draft));
		aborted.GetBaseException().Message.ShouldBe("Synthetic ESIC failure after snapshot/review writes and before commit.");
		failure.ObservedDeletedSnapshot.ShouldBeTrue("Snapshot deletion executed inside the failing transaction.");
		failure.ObservedDeletedReview.ShouldBeTrue("Review deletion executed inside the failing transaction.");
		var after = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url);
		after.History.Count.ShouldBe(before.History.Count);
		after.Current!.Id.ShouldBe(before.Current!.Id);
		after.Current.IsCancelled.ShouldBeFalse();
		(await StoredPayrollAsync()).ShouldBe(payrollBefore);
		(await Client.PutAsJsonAsync(Url, draft)).StatusCode.ShouldBe(HttpStatusCode.OK);
	}

	private async Task<string> StoredPayrollAsync()
	{
		string? result = null;
		await InTenantAsync(async services => {
			result = JsonSerializer.Serialize(new {
				Snapshots = (await services.GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>().GetListAsync()).OrderBy(x => x.Id),
				Reviews = (await services.GetRequiredService<IRepository<PayrollRunReview, Guid>>().GetListAsync()).OrderBy(x => x.Id),
				Slips = (await services.GetRequiredService<IRepository<SalarySlip, Guid>>().GetListAsync()).OrderBy(x => x.Id)
			}, JsonOptions);
		});
		return result!;
	}
}
