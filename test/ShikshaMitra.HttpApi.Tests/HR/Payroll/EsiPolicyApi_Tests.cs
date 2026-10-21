using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
public class EsiPolicyApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private const string Url = "/api/payroll/statutory-schemes/esi/policies";
	[Fact]
	public async Task Selection_catalogue_does_not_grant_policy_management_and_is_tenant_isolated()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var created = await CreateAsync();
		using var anonymous = CreateAnonymousClient();
		(await anonymous.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		using var selection = Factory.CreateClient();
		selection.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View);
		(await selection.GetAsync(Url + "/options")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await selection.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await selection.PostAsJsonAsync(Url, Draft())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var otherTenant = Factory.CreateClient();
		otherTenant.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await otherTenant.GetAsync(Url + "/options")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}
	[Fact]
	public async Task Policy_versions_change_preview_and_template_save_binds_the_policy()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var policy = await CreateAsync();
		var catalog = await GetAndReadAsync<SalaryTemplateComponentCatalogDto>("/api/payroll/salary-templates/component-catalog");
		var earning = catalog.Items.First(x => x.LineKind == "EARNING");
		var request = new PreviewSalaryTemplateRequestDto { SalaryBasis = "GROSS", MonthlyAmount = 10000m,
			EffectiveMonth = new(2046, 4, 1), EsiPolicyId = policy.Id, Rules = [new SalaryTemplateRuleDto
			{ ComponentId = earning.ComponentId, SemanticType = "BASIC", LineKind = "EARNING", FormulaType = "PERCENTAGE_OF_GROSS", Value = 100m }] };
		var first = await PostAndReadAsync<SalaryTemplatePreviewDto>("/api/payroll/salary-templates/preview", request);
		first.Calculation.EmployeeStateInsurance.PolicyId.ShouldBe(policy.Id);
		first.Calculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(75m);
		first.Calculation.EmployeeStateInsurance.EmployerContribution.ShouldBe(325m);
		var update = Draft();
		update.EffectiveFrom = new(2046, 5, 1); update.EmployeePercentage = 1m; update.EmployerPercentage = 4m;
		update.ConcurrencyStamp = policy.ConcurrencyStamp;
		var changed = await PostAndReadAsync<EsiPolicyDto>($"{Url}/{policy.Id}/versions", update);
		changed.Versions.Count.ShouldBe(2);
		changed.Versions.Single(x => x.Version == 1).AppliesUntil.ShouldBe(new DateTime(2046, 4, 30));
		request.EffectiveMonth = new(2046, 5, 1);
		var second = await PostAndReadAsync<SalaryTemplatePreviewDto>("/api/payroll/salary-templates/preview", request);
		second.Calculation.EmployeeStateInsurance.EmployeeContribution.ShouldBe(100m);
		second.Calculation.EmployeeStateInsurance.EmployerContribution.ShouldBe(400m);
		var save = new SaveSalaryTemplateDto { Name = "ESIC policy round trip " + Guid.NewGuid().ToString("N")[..8], Rules = request.Rules,
			DefaultSalaryBasis = "GROSS", DefaultEsiPolicyId = policy.Id, SampleMonthlyAmount = 10000m, SampleEffectiveMonth = request.EffectiveMonth,
			PreviewToken = second.PreviewToken, IdempotencyKey = Guid.NewGuid().ToString("N") };
		var template = await PostAndReadAsync<SalaryTemplateDto>("/api/payroll/salary-templates", save);
		var savedKey = save.IdempotencyKey;
		template.EditableVersion.DefaultEsiPolicyId.ShouldBe(policy.Id);
		save.IdempotencyKey = Guid.NewGuid().ToString("N"); save.DefaultEsiManagementMode = EsiManagement.External;
		save.DefaultEsiPolicyId = null; save.DefaultEsiExternalReason = "External provider";
		(await Client.PostAsJsonAsync("/api/payroll/salary-templates", save)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		request.EsiManagementMode = EsiManagement.External; request.EsiPolicyId = null; request.EsiExternalReason = "External provider";
		var external = await PostAndReadAsync<SalaryTemplatePreviewDto>("/api/payroll/salary-templates/preview", request);
		external.Calculation.EmployeeStateInsurance.CoverageDecision.ShouldBe("EXTERNALLY_MANAGED");
		external.Calculation.Deductions.ShouldNotContain(x => x.SemanticType == "EMPLOYEE_STATE_INSURANCE");
		external.Calculation.EmployerContributions.ShouldNotContain(x => x.Code.StartsWith("EMPLOYER_ESI"));
		external.Calculation.EmployerCost.ShouldBe(10000m);
		save.IdempotencyKey = savedKey;
		save.PreviewToken = external.PreviewToken;
		(await Client.PostAsJsonAsync("/api/payroll/salary-templates", save)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}
	[Fact]
	public async Task Retries_conflicts_overlaps_and_archived_assignments_are_explicit()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var input = Draft();
		var policy = await PostAndReadAsync<EsiPolicyDto>(Url, input);
		(await PostAndReadAsync<EsiPolicyDto>(Url, input)).Id.ShouldBe(policy.Id);
		input.EmployeePercentage = 2m;
		(await Client.PostAsJsonAsync(Url, input)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var version = Draft(); version.ConcurrencyStamp = policy.ConcurrencyStamp;
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/versions", version)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		version.EffectiveFrom = new(2046, 8, 1);
		var saved = await PostAndReadAsync<EsiPolicyDto>($"{Url}/{policy.Id}/versions", version);
		version.IdempotencyKey = Guid.NewGuid(); version.EffectiveFrom = new(2046, 9, 1);
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/versions", version)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await Client.PostAsJsonAsync($"{Url}/{policy.Id}/archive", new { saved.ConcurrencyStamp })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
		var all = await GetAndReadAsync<List<EsiPolicyDto>>(Url);
		all.Single(x => x.Id == policy.Id).IsArchived.ShouldBeTrue();
		all.Single(x => x.Id == policy.Id).Versions.Count.ShouldBe(2);
	}
	private async Task<EsiPolicyDto> CreateAsync() => await PostAndReadAsync<EsiPolicyDto>(Url, Draft());
	private static CreateEsiPolicyDto Draft() => new() { Name = "ESIC Test " + Guid.NewGuid().ToString("N")[..8], Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
		EffectiveFrom = new(2046, 1, 1), SourceUrl = "https://example.invalid/esic-test-policy", SourceVerifiedOn = new(2026, 10, 1), IdempotencyKey = Guid.NewGuid(),
		CalculationSettings = new() { EligibilityBasis = "GROSS", CalculationBasis = "GROSS" } };
}
