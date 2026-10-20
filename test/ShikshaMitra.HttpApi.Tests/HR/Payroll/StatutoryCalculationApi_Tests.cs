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
	public async Task Independent_bases_preserve_history_validate_basic_and_fingerprint_each_source()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(EsiUrl);
		var month = DateTime.ParseExact(settings.SuggestedEffectiveMonth, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)
			.AddMonths(4).ToString("yyyy-MM");
		var effective = settings.History.Where(x => !x.IsCancelled && string.Compare(x.EffectiveMonth, month) <= 0)
			.OrderByDescending(x => x.EffectiveMonth).First();
		var basic = await PayrollTestHelper.CreateEarningComponentAsync(Client, "IndependentEsiBasic");
		var input = new SaveEsiConfigurationDto
		{
			Enabled = true,
			EffectiveMonth = month,
			ConcurrencyStamp = effective.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid(),
			CalculationSettings = new() { EligibilityBasis = "GROSS", CalculationBasis = "COMPONENT" }
		};
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		input.CalculationSettings = input.CalculationSettings with { CalculationComponentId = Guid.NewGuid() };
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		input.CalculationSettings = input.CalculationSettings with { CalculationComponentId = basic.Id };
		var response = await Client.PutAsJsonAsync(EsiUrl, input);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!;
		var version = saved.History.Single(x => x.EffectiveMonth == month && !x.IsCancelled);
		version.CalculationSettings.EligibilityBasis.ShouldBe("GROSS");
		version.CalculationSettings.CalculationBasis.ShouldBe("COMPONENT");
		version.CalculationSettings.CalculationComponentId.ShouldBe(basic.Id);
		foreach (var original in settings.History)
		{
			var retained = saved.History.Single(x => x.Id == original.Id);
			retained.CalculationSettings.ShouldBe(original.CalculationSettings);
			retained.ConcurrencyStamp.ShouldBe(original.ConcurrencyStamp);
		}
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.OK);
		input.CalculationSettings = input.CalculationSettings with { EligibilityBasis = "COMPONENT", EligibilityComponentId = basic.Id };
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		input.CalculationSettings = input.CalculationSettings with { EligibilityBasis = "GROSS", EligibilityComponentId = null, CalculationBasis = "GROSS", CalculationComponentId = null };
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await Client.PutAsJsonAsync(EsiUrl, new
		{
			Enabled = true,
			EffectiveMonth = month,
			ConcurrencyStamp = effective.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid(),
			CalculationSettings = new { WageBasis = "GROSS" }
		})).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
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
			var logos = ActivatorUtilities.CreateInstance<ShikshaMitra.HR.Payroll.SalarySlips.PayslipLogoService>(services);
			logos.LazyServiceProvider = lazy;
			await Should.ThrowAsync<AbpAuthorizationException>(() => pf.GetAsync());
			await Should.ThrowAsync<AbpAuthorizationException>(() => pf.SaveAsync(new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => esi.SaveAsync(new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => salary.CalculateAsync(Guid.NewGuid(), new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => enrollment.SaveAsync(Guid.NewGuid(), new()));
			await Should.ThrowAsync<AbpAuthorizationException>(() => logos.UploadAsync("image/png", []));
		}
		finally { http.HttpContext = prior; }
	}
	[Fact]
	public async Task Registration_readiness_and_logo_upload_use_payroll_scope_gateways()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var staff = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "RegistrationScopeReview");
		const string readiness = "/api/payroll/statutory-profile/readiness?search=RegistrationScopeReview&pageSize=1";
		var page = await GetAndReadAsync<PayrollRegistrationReadinessDto>(readiness);
		page.Items.ShouldContain(x => x.EmployeeId == staff);
		page.Items.Count.ShouldBe(1);
		using var noScope = Factory.CreateClient();
		noScope.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		(await noScope.GetAsync(readiness)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		(await denied.GetAsync(readiness)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var upload = new System.Net.Http.MultipartFormDataContent();
		using var image = new System.Net.Http.ByteArrayContent(Convert.FromBase64String(
			"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9xkAAAAASUVORK5CYII="));
		image.Headers.ContentType = new("image/png");
		upload.Add(image, "file", "synthetic-logo.png");
		(await noScope.PostAsync("/api/payroll/payslip-settings/logo", upload)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}
	[Fact]
	public async Task Compensation_allows_audited_PF_override_but_requires_real_salary_rules()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
		var coverage = await GetAndReadAsync<PfCoverageSettingsDto>(PfUrl);
		await EsiPayrollWorkflowScenario.RequireSuccess(await Client.PutAsJsonAsync(PfUrl, new SavePfCoverageDto {
			EffectiveMonth = month, IsCovered = true, EvidenceReference = "Synthetic PF registration",
			ExpectedConfigurationId = coverage.History.FirstOrDefault(x => !x.IsCancelled && x.EffectiveMonth <= month)?.Id, IdempotencyKey = Guid.NewGuid() }));
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "PfEnrollmentPolicy");
		var template = (await GetAndReadAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active")).Items
			.First(x => x.EditableVersion.Rules.Any(rule => rule.FormulaType == "EMPLOYEE_PROVIDENT_FUND"));
		var profileUrl = $"/api/employees/{employeeId}/payroll/statutory-profile";
		var profile = await GetAndReadAsync<EmployeeStatutoryProfileDto>(profileUrl);
		var input = new SaveEmployeeCompensationDto { TemplateVersionId = template.EditableVersion.Id,
			EsiManagementMode = template.EditableVersion.DefaultEsiManagementMode, EsiPolicyId = template.EditableVersion.DefaultEsiPolicyId,
			EsiExternalReason = template.EditableVersion.DefaultEsiExternalReason,
			SalaryBasis = "GROSS", ProtectionPolicy = "PROTECT_GROSS", MonthlyAmount = 25000m, EffectiveMonth = month,
			PfApplicable = false, PfEnrollment = new() { Membership = "NOT_MEMBER" }, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var url = $"/api/payroll/employees/{employeeId}/compensation";
		var missingRule = await Client.PutAsJsonAsync(url, input);
		missingRule.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingRule.Content.ReadAsStringAsync()).ShouldContain("PfRuleNotFound");
		await EsiPayrollWorkflowScenario.RequireSuccess(await Client.PutAsJsonAsync(profileUrl, new SaveEmployeeStatutoryProfileDto {
			Profile = profile.Profile with { ApplyPf = false, PfDisabledReason = "Pending school registration resolution" },
			ConcurrencyStamp = profile.ConcurrencyStamp, EffectiveMonth = month.ToString("yyyy-MM"), UpdateEsic = false, IdempotencyKey = Guid.NewGuid() }));
		var savedResponse = await Client.PutAsJsonAsync(url, input); await EsiPayrollWorkflowScenario.RequireSuccess(savedResponse);
		var saved = (await savedResponse.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		saved.PfRuleId.ShouldBeNull(); saved.PfEligibility!.Status.ShouldBe("MANDATORY");
		saved.PfEligibility.CanSave.ShouldBeTrue();
		var quote = System.Text.Json.JsonSerializer.Deserialize<SalaryTemplateCalculationDto>(saved.CalculationSnapshotJson!)!;
		quote.StatutoryApplication!.ProvidentFund.AppliedEmployeeAmount.ShouldBe(0);
		quote.StatutoryApplication.ProvidentFund.WarningCode.ShouldBe("PF_APPLY_DISABLED");
		quote.StatutoryApplication.ProvidentFund.Eligible.ShouldBeTrue();
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.PayrollManagement.CompensationRevisions.View");
		(await denied.PutAsJsonAsync(profileUrl, new SaveEmployeeStatutoryProfileDto { Profile = profile.Profile, IdempotencyKey = Guid.NewGuid(), UpdateEsic = false })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherTenant = Factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await otherTenant.GetAsync(profileUrl)).IsSuccessStatusCode.ShouldBeFalse();
	}
	[Fact]
	public async Task Calculation_changes_require_the_exact_administrator_capability()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var settings = await GetAndReadAsync<EsiConfigurationSettingsDto>(EsiUrl);
		using var denied = Factory.CreateClient();
		denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"HR.PayrollManagement.Settings.StatutorySchemes.View,HR.PayrollManagement.Settings.StatutorySchemes.Edit,HR.PayrollManagement.Settings.StatutorySchemes.Activate");
		(await denied.PutAsJsonAsync(EsiUrl, new SaveEsiConfigurationDto
		{
			Enabled = true,
			EffectiveMonth = settings.SuggestedEffectiveMonth,
			ConcurrencyStamp = settings.Current!.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid()
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
		var input = new SaveEsiConfigurationDto
		{
			Enabled = true,
			EffectiveMonth = month,
			ConcurrencyStamp = effective.ConcurrencyStamp,
			IdempotencyKey = Guid.NewGuid(),
			CalculationSettings = new() { EligibilityBasis = "GROSS", CalculationBasis = "CLASSIFIED_EARNINGS", EmployeePercentage = 0.5m, EmployerPercentage = 4m }
		};
		(await Client.PutAsJsonAsync(EsiUrl, input)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		input.CalculationSettings = input.CalculationSettings with { OverrideReason = "Documented policy", EvidenceReference = "Policy reference" };
		var response = await Client.PutAsJsonAsync(EsiUrl, input);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var saved = (await response.Content.ReadFromJsonAsync<EsiConfigurationSettingsDto>(JsonOptions))!;
		var version = saved.History.Single(x => x.EffectiveMonth == month && !x.IsCancelled);
		version.CalculationSettings.EmployeePercentage.ShouldBe(0.5m);
		version.CalculationSettings.EligibilityBasis.ShouldBe("GROSS");
		version.CalculationSettings.CalculationBasis.ShouldBe("CLASSIFIED_EARNINGS");
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
