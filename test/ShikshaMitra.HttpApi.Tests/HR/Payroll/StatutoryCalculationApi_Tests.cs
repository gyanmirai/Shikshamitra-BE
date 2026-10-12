using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Employees;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class StatutoryCalculationApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string EsiUrl = "/api/payroll/statutory-schemes/esi/configuration";
	private const string PfUrl = "/api/payroll/pf-rules/coverage";
	[Fact]
	public async Task Direct_application_calls_cannot_bypass_calculation_or_enrollment_permissions()
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var http = services.GetRequiredService<IHttpContextAccessor>();
		var prior = http.HttpContext;
		var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("SyntheticDeniedCaller")) };
		context.Request.Headers[AlwaysGrantPermissionChecker.TestPermissionsHeader] = "__none__";
		http.HttpContext = context;
		try
		{
			var lazy = services.GetRequiredService<IAbpLazyServiceProvider>();
			var pf = ActivatorUtilities.CreateInstance<PfCoverageAppService>(services);
			pf.LazyServiceProvider = lazy;
			var esi = ActivatorUtilities.CreateInstance<EsiConfigurationAppService>(services);
			esi.LazyServiceProvider = lazy;
			var salary = ActivatorUtilities.CreateInstance<SalaryTemplateAppService>(services);
			salary.LazyServiceProvider = lazy;
			var enrollment = ActivatorUtilities.CreateInstance<CompensationRevisionAppService>(services);
			enrollment.LazyServiceProvider = lazy;
			await Should.ThrowAsync<AbpAuthorizationException>(() => pf.GetAsync());
			await Should.ThrowAsync<AbpAuthorizationException>(() => pf.SaveAsync(new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => esi.SaveAsync(new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => salary.CalculateAsync(Guid.NewGuid(), new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => enrollment.SaveAsync(Guid.NewGuid(), new()));
		}
		finally { http.HttpContext = prior; }
	}
	[Fact]
	public async Task Compensation_rejects_missing_mandatory_enrollment_and_preserves_voluntary_membership()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
		var coverage = await GetAndReadAsync<PfCoverageSettingsDto>(PfUrl);
		var current = coverage.History.FirstOrDefault(x => !x.IsCancelled && x.EffectiveMonth <= month);
		var school = await Client.PutAsJsonAsync(PfUrl, new SavePfCoverageDto { EffectiveMonth = month, IsCovered = true,
			EvidenceReference = "Synthetic PF registration", ExpectedConfigurationId = current?.Id, IdempotencyKey = Guid.NewGuid() });
		school.StatusCode.ShouldBe(HttpStatusCode.OK, await school.Content.ReadAsStringAsync());
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PfEnrollmentPolicy");
		var template = (await GetAndReadAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active")).Items
			.First(x => x.EditableVersion.Rules.Any(rule => rule.FormulaType == "EMPLOYEE_PROVIDENT_FUND"));
		var input = new SaveEmployeeCompensationDto { TemplateVersionId = template.EditableVersion.Id,
			SalaryBasis = "GROSS", ProtectionPolicy = "PROTECT_GROSS", MonthlyAmount = 12000m,
			EffectiveMonth = month, PfApplicable = false, PfEnrollment = new() { Membership = "NOT_MEMBER" },
			IdempotencyKey = Guid.NewGuid().ToString("N") };
		var url = $"/api/payroll/employees/{employeeId}/compensation";
		var required = await Client.PutAsJsonAsync(url, input);
		required.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await required.Content.ReadAsStringAsync()).ShouldContain("PF_ENROLLMENT_REQUIRED");
		input.MonthlyAmount = 60000m;
		input.PfApplicable = true;
		input.PfRuleId = template.EditableVersion.DefaultPfRuleId ?? (await PayrollTestHelper.CreatePfRuleAsync(Client, "VoluntaryEnrollment")).Id;
		var missingConsent = await Client.PutAsJsonAsync(url, input);
		missingConsent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingConsent.Content.ReadAsStringAsync()).ShouldContain("PF_CONSENT_REQUIRED");
		input.PfEnrollment = input.PfEnrollment with { ConsentReference = "Synthetic employee consent" };
		var saved = await Client.PutAsJsonAsync(url, input);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		var revision = (await saved.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		revision.PfEnrollment.ConsentReference.ShouldBe("Synthetic employee consent");
		input.ExpectedRevision = revision.RevisionNumber;
		input.IdempotencyKey = Guid.NewGuid().ToString("N");
		input.PfApplicable = false;
		input.PfRuleId = null;
		var continuing = await Client.PutAsJsonAsync(url, input);
		continuing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await continuing.Content.ReadAsStringAsync()).ShouldContain("PF_ENROLLMENT_REQUIRED");
	}
	[Fact]
	public async Task Calculation_changes_require_the_exact_administrator_capability()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(EsiUrl);
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"HR.PayrollManagement.Settings.StatutorySchemes.View,HR.PayrollManagement.Settings.StatutorySchemes.Edit,HR.PayrollManagement.Settings.StatutorySchemes.Activate");
		(await denied.PutAsJsonAsync(EsiUrl, new SaveEsiConfigurationDto {
			Enabled = true, EffectiveMonth = settings.SuggestedEffectiveMonth,
			ConcurrencyStamp = settings.Current!.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid()
		})).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task Rate_overrides_are_scheduled_documented_and_idempotent()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(EsiUrl);
		var month = DateTime.ParseExact(settings.SuggestedEffectiveMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture).AddMonths(2).ToString("yyyy-MM");
		var effective = settings.History.Where(x => !x.IsCancelled && string.Compare(x.EffectiveMonth, month) <= 0)
			.OrderByDescending(x => x.EffectiveMonth).First();
		var input = new SaveEsiConfigurationDto { Enabled = true, EffectiveMonth = month,
			ConcurrencyStamp = effective.ConcurrencyStamp, IdempotencyKey = Guid.NewGuid(),
			CalculationSettings = new() { WageBasis = "GROSS", EmployeePercentage = 0.5m, EmployerPercentage = 4m }
		};
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		input.CalculationSettings = input.CalculationSettings with { OverrideReason = "Documented policy", EvidenceReference = "Policy reference" };
		var response = await Client.PutAsJsonAsync(EsiUrl, input);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!;
		var version = saved.History.Single(x => x.EffectiveMonth == month && !x.IsCancelled);
		version.CalculationSettings.EmployeePercentage.ShouldBe(0.5m);
		version.CalculationSettings.OverrideReason.ShouldBe("Documented policy");
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.OK);
		input.CalculationSettings = input.CalculationSettings with { EmployeePercentage = 0.25m };
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}
	[Fact]
	public async Task Pf_school_coverage_requires_permission_scope_evidence_and_stale_write_protection()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(4);
		var input = new SavePfCoverageDto { EffectiveMonth = month, IsCovered = true, IdempotencyKey = Guid.NewGuid() };
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.PayrollManagement.Settings.PfRule.View");
		(await denied.PutAsJsonAsync(PfUrl, input)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		input.EvidenceReference = "Registration evidence";
		var initial = await GetAndReadAsync<PfCoverageSettingsDto>(PfUrl);
		input.ExpectedConfigurationId = initial.History.Where(x => !x.IsCancelled && x.EffectiveMonth <= month).OrderByDescending(x => x.EffectiveMonth).FirstOrDefault()?.Id;
		var response = await Client.PutAsJsonAsync(PfUrl, input);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		(await Client.PutAsJsonAsync(PfUrl, input)).StatusCode.ShouldBe(HttpStatusCode.OK);
		input.IdempotencyKey = Guid.NewGuid();
		(await Client.PutAsJsonAsync(PfUrl, input)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}
}
