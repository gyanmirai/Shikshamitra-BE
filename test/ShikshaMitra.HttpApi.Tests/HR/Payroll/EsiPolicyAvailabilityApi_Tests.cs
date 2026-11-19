using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Seeders;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

// Each scenario owns its synthetic database. No tenant connection is used.
[Collection(ApiTestCollection.Name)]
public class EsiPolicyAvailabilityApi_Tests
{
	[Fact]
	public async Task Staff_review_requires_only_the_selected_in_platform_policy()
	{
		using var factory = new EsiHistoricalTestFactory();
		var scenario = new EsiPolicyAvailabilityScenario(factory);
		await scenario.CheckStaffReviewAsync();
	}

	[Fact]
	public async Task Seeding_named_v1_does_not_suppress_default_and_preserves_existing_history()
	{
		using var factory = new EsiHistoricalTestFactory();
		var scenario = new EsiPolicyAvailabilityScenario(factory);
		await scenario.CheckSeedingAsync();
	}

	[Fact]
	public async Task Seeding_does_not_mask_ambiguous_legacy_versions()
	{
		using var factory = new EsiHistoricalTestFactory();
		await new EsiPolicyAvailabilityScenario(factory).CheckAmbiguousLegacyAsync();
	}
}

internal class EsiPolicyAvailabilityScenario(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/payroll/statutory-schemes/esi";
	private static readonly Guid Tenant = Guid.Parse(TestAuthHandler.TestTenantId);
	private readonly Guid _policyId = Guid.NewGuid();
	private async Task InTenantAsync(Func<IServiceProvider, Task> action)
	{
		using var scope = Factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Tenant);
		using var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
		await action(scope.ServiceProvider);
		await unit.CompleteAsync();
	}

	private StatutoryRuleVersion Rule(Guid? policy, int version, DateTime start, DateTime? end = null) =>
		new(Guid.NewGuid(), Tenant, "ESI", "IN-GJ", version, start, end, 10, 21000m, 25000m,
			1m, 4m, 50m, 15m, StatutoryRoundingPolicy.NextHigherRupee, "https://example.invalid/test", start,
			esiPolicyId: policy, calculationSettings: new() { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" });

	private async Task InitializeAsync()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await InTenantAsync(async services =>
		{
			var rules = services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>();
			foreach (var rule in await rules.GetListAsync(x => x.SchemeCode == "ESI"))
				await rules.HardDeleteAsync(rule);
			await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().InsertAsync(
				new EsiPolicy(_policyId, Tenant, "ESIC HS Policy", "HS_POLICY"));
			await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().InsertAsync(
				Rule(_policyId, 1, new(2026, 12, 1), new(2027, 1, 31)));
		});
	}

	private async Task AssignAsync(Guid? policy, bool external = false)
	{
		await InTenantAsync(async services =>
		{
			var revisions = services.GetRequiredService<IRepository<EmployeeCompensationRevision, Guid>>();
			await services.GetRequiredService<IRepository<EmployeeCompensationRevisionLine, Guid>>().DeleteAsync(x => true);
			await revisions.DeleteAsync(x => true);
			foreach (var employee in await services.GetRequiredService<IEmployeeRepository>().GetListAsync())
			{
				var revision = new EmployeeCompensationRevision(Guid.NewGuid(), Tenant, employee.Id, 1, new(2026, 11, 1));
				revision.ReplaceDraft(new(2026, 11, 1), SalaryInputBasis.Gross, CompensationProtectionPolicy.ProtectGross,
					10000m, 10000m, 10000m, 9900m, 10400m, null, null, null, null, "IN-GJ", "{}");
				if (external) revision.SetEsiAssignment(EsiManagement.External, null, "External provider");
				else if (policy.HasValue) revision.SetEsiAssignment(EsiManagement.InPlatform, policy, null);
				// A null in-platform ID represents a legacy unassigned revision.
				revision.MakeEffective(Guid.Parse(TestAuthHandler.TestUserId), DateTime.UtcNow);
				await revisions.InsertAsync(revision);
				await services.GetRequiredService<IRepository<EmployeeCompensationRevisionLine, Guid>>().InsertAsync(
					new EmployeeCompensationRevisionLine(Guid.NewGuid(), Tenant, revision.Id, employee.Id, null,
						"Basic Salary", null, 10000m, true, PayrollComponentSemanticType.Basic, default, 0));
			}
		});
	}

	private async Task<StatutoryImpactPreviewDto> PreviewAsync(string month = "12-2026") =>
		await PostAndReadAsync<StatutoryImpactPreviewDto>(Url + "/impact-preview", new StatutoryImpactPreviewRequestDto { MonthYear = month });

	private async Task AssertNotEffectiveAsync(string month)
	{
		var response = await Client.PostAsJsonAsync(Url + "/impact-preview", new StatutoryImpactPreviewRequestDto { MonthYear = month });
		response.IsSuccessStatusCode.ShouldBeFalse();
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EsiPolicyNotEffective);
	}

	internal async Task CheckStaffReviewAsync()
	{
		await InitializeAsync();
		await AssignAsync(_policyId);
		var first = await PreviewAsync();
		first.Items.ShouldNotBeEmpty();
		first.Items.ShouldAllBe(x => x.EmployeeContribution == 100m && x.EmployerContribution == 400m);
		(await PreviewAsync()).PreviewFingerprint.ShouldBe(first.PreviewFingerprint);
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(Url + "/configuration");
		var save = await Client.PutAsJsonAsync(Url + "/configuration", new SaveEsiConfigurationDto {
			EffectiveMonth = "2026-12", Enabled = true, ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid() });
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		// The school rule endpoint still requires the actual default.
		var school = await Client.GetAsync(Url + "/rule?effectiveOn=2026-12-01");
		school.IsSuccessStatusCode.ShouldBeFalse();
		(await school.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.EsiPolicyNotEffective);
		await AssertNotEffectiveAsync("11-2026");
		await AssertNotEffectiveAsync("02-2027");
		await InTenantAsync(async services => await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>()
			.InsertAsync(Rule(_policyId, 2, new(2027, 3, 1))));
		await AssertNotEffectiveAsync("02-2027");
		(await PreviewAsync("03-2027")).Items.ShouldNotBeEmpty();
		var missing = Guid.NewGuid();
		await InTenantAsync(async services => await services.GetRequiredService<IRepository<EsiPolicy, Guid>>()
			.InsertAsync(new EsiPolicy(missing, Tenant, "No effective version", "NO_VERSION")));
		await AssignAsync(missing);
		await AssertNotEffectiveAsync("12-2026");
		await AssignAsync(null);
		await AssertNotEffectiveAsync("12-2026");
		await InTenantAsync(async services => await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().DeleteAsync(x => true));
		await AssignAsync(null, external: true);
		(await PreviewAsync()).Items.ShouldAllBe(x => x.CoverageDecisionCode == "EXTERNALLY_MANAGED" && x.CanCalculate && x.EmployeeContribution == 0m);
		await InTenantAsync(async services => await services.GetRequiredService<IEmployeeRepository>().DeleteAsync(x => true));
		(await PreviewAsync()).Summary.EmployeeCount.ShouldBe(0);
	}

	internal async Task CheckSeedingAsync()
	{
		await InitializeAsync();
		await InTenantAsync(async services => await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant)));
		await InTenantAsync(async services => await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant)));
		await InTenantAsync(async services =>
		{
			var policy = await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(x => x.IsDefault);
			var rules = await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().GetListAsync(x => x.SchemeCode == "ESI");
			rules.Count(x => x.EsiPolicyId == policy.Id).ShouldBe(1);
			rules.Single(x => x.EsiPolicyId == _policyId).EffectiveFrom.ShouldBe(new DateTime(2026, 12, 1));
		});
		(await Client.GetAsync(Url + "/rule?effectiveOn=2026-12-01")).StatusCode.ShouldBe(HttpStatusCode.OK);
		var orphan = Rule(null, 1, new(2026, 12, 1));
		await InTenantAsync(async services => await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().InsertAsync(orphan));
		await InTenantAsync(async services => await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant)));
		await InTenantAsync(async services => (await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().GetAsync(orphan.Id)).EsiPolicyId.ShouldBeNull());
		await AssignAsync(null);
		await AssertNotEffectiveAsync("12-2026");
		await InTenantAsync(async services =>
		{
			var policy = await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(x => x.IsDefault);
			var rules = services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>();
			await rules.DeleteAsync(x => x.EsiPolicyId == policy.Id);
			await rules.InsertAsync(Rule(policy.Id, 2, new(2027, 3, 1)), autoSave: true);
		});
		await InTenantAsync(async services =>
		{
			await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant));
		});
		await InTenantAsync(async services =>
		{
			var policy = await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(x => x.IsDefault);
			(await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().GetListAsync(x => x.EsiPolicyId == policy.Id)).Count.ShouldBe(1);
		});
		// Intentional future/expired default history must never be backfilled through a gap.
		(await Client.GetAsync(Url + "/rule?effectiveOn=2026-12-01")).IsSuccessStatusCode.ShouldBeFalse();
		await InTenantAsync(async services =>
		{
			var policy = await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(x => x.IsDefault);
			var rules = services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>();
			await rules.DeleteAsync(x => x.EsiPolicyId == policy.Id, autoSave: true);
			await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant));
			(await rules.AnyAsync(x => x.EsiPolicyId == policy.Id)).ShouldBeFalse();
		});
	}

	internal async Task CheckAmbiguousLegacyAsync()
	{
		await InitializeAsync();
		await InTenantAsync(async services =>
		{
			var rules = services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>();
			await rules.InsertManyAsync([Rule(null, 1, new(2026, 12, 1)), Rule(null, 2, new(2026, 12, 1))], autoSave: true);
			await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant));
			var policy = await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(x => x.IsDefault);
			(await rules.AnyAsync(x => x.EsiPolicyId == policy.Id)).ShouldBeFalse();
			(await rules.GetListAsync(x => x.EsiPolicyId == null)).Count.ShouldBe(2);
		});
		(await Client.GetAsync(Url + "/rule?effectiveOn=2026-12-01")).IsSuccessStatusCode.ShouldBeFalse();
		var legacyId = Guid.NewGuid();
		await InTenantAsync(async services =>
		{
			var rules = services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>();
			foreach (var rule in await rules.GetListAsync(x => x.EsiPolicyId == null)) await rules.HardDeleteAsync(rule);
			var legacy = Rule(null, 3, new(2027, 3, 1));
			legacyId = legacy.Id;
			await rules.InsertAsync(legacy, autoSave: true);
			await services.GetRequiredService<PayrollStatutorySettingsBackfillSeederContributor>().SeedAsync(new DataSeedContext(Tenant));
		});
		await InTenantAsync(async services =>
		{
			var policy = await services.GetRequiredService<IRepository<EsiPolicy, Guid>>().GetAsync(x => x.IsDefault);
			(await services.GetRequiredService<IRepository<StatutoryRuleVersion, Guid>>().GetListAsync(x => x.EsiPolicyId == policy.Id)).Single().Id.ShouldBe(legacyId);
		});
	}
}
