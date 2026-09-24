using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Controllers.HR.Payroll;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class SalaryTemplateApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/salary-templates";

	public SalaryTemplateApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	public async Task Should_reject_circular_remainder_earning_before_saving_template()
	{
		var catalog = await Client.GetFromJsonAsync<SalaryTemplateComponentCatalogDto>($"{BaseUrl}/component-catalog", JsonOptions);
		var earnings = catalog!.Items.Where(x => x.LineKind == "EARNING").Take(3).ToArray();
		earnings.Length.ShouldBe(3);
		var request = Save($"Circular Gross {Guid.NewGuid():N}", earnings[0].ComponentId);
		request.Rules[0].Value = 60m;
		request.Rules.Add(new SalaryTemplateRuleDto
		{
			ComponentId = earnings[1].ComponentId, LineKind = "EARNING", SemanticType = "BALANCING_EARNING",
			FormulaType = "REMAINDER_OF_GROSS", DisplayOrder = 1
		});
		request.Rules.Add(new SalaryTemplateRuleDto
		{
			ComponentId = earnings[2].ComponentId, LineKind = "EARNING", SemanticType = "ORDINARY",
			FormulaType = "PERCENTAGE_OF_COMPONENT", Value = 10m, BasisComponentId = earnings[1].ComponentId, DisplayOrder = 2
		});
		var response = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);
		response.IsSuccessStatusCode.ShouldBeFalse(await response.Content.ReadAsStringAsync());
		(await response.Content.ReadAsStringAsync()).ShouldContain("HR:Payroll:SalaryTemplateDependencyCycle");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_create_a_complete_active_salary_template()
	{
		var catalog = await Client.GetFromJsonAsync<SalaryTemplateComponentCatalogDto>(
			$"{BaseUrl}/component-catalog", JsonOptions);
		var basic = catalog!.Items.First(x => x.LineKind == "EARNING");
		var input = Save($"Active Composer {Guid.NewGuid():N}", basic.ComponentId);
		var previewResponse = await Client.PostAsJsonAsync($"{BaseUrl}/preview", Preview(input), JsonOptions);
		previewResponse.IsSuccessStatusCode.ShouldBeTrue(await previewResponse.Content.ReadAsStringAsync());
		input.PreviewToken = (await previewResponse.Content.ReadFromJsonAsync<SalaryTemplatePreviewDto>(JsonOptions))!.PreviewToken;
		var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var template = await response.Content.ReadFromJsonAsync<SalaryTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.Status.ShouldBe("ACTIVE");
		template.ActiveVersion.ShouldBe(1);
		template.EditableVersion.Status.ShouldBe("ACTIVE");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_return_active_component_catalog()
	{
		var response = await Client.GetAsync($"{BaseUrl}/component-catalog");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var catalog = await response.Content.ReadFromJsonAsync<SalaryTemplateComponentCatalogDto>(JsonOptions);
		catalog.ShouldNotBeNull();
		catalog.Items.ShouldAllBe(item =>
			item.ComponentId != Guid.Empty &&
			!string.IsNullOrWhiteSpace(item.Name) &&
			(item.LineKind == "EARNING" || item.LineKind == "DEDUCTION"));
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_switch_defaults_in_both_directions_and_archive_with_replacement()
	{
		var catalog = await Client.GetFromJsonAsync<SalaryTemplateComponentCatalogDto>($"{BaseUrl}/component-catalog", JsonOptions);
		var earning = catalog!.Items.First(x => x.LineKind == "EARNING");
		async Task<SalaryTemplateDto> CreateAsync()
		{
			var input = Save($"Default switch {Guid.NewGuid():N}", earning.ComponentId);
			var preview = await Client.PostAsJsonAsync($"{BaseUrl}/preview", Preview(input), JsonOptions);
			preview.EnsureSuccessStatusCode();
			input.PreviewToken = (await preview.Content.ReadFromJsonAsync<SalaryTemplatePreviewDto>(JsonOptions))!.PreviewToken;
			var response = await Client.PostAsJsonAsync(BaseUrl, input, JsonOptions);
			response.EnsureSuccessStatusCode();
			return (await response.Content.ReadFromJsonAsync<SalaryTemplateDto>(JsonOptions))!;
		}
		var first = await CreateAsync();
		var second = await CreateAsync();
		foreach (var id in new[] { first.Id, second.Id, first.Id })
		{
			var response = await Client.PostAsync($"{BaseUrl}/{id}/set-default", null);
			response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
			var result = await Client.GetFromJsonAsync<SalaryTemplateDto>($"{BaseUrl}/{id}", JsonOptions);
			result!.IsDefault.ShouldBeTrue();
		}
		var archive = await Client.DeleteAsync($"{BaseUrl}/{first.Id}?replacementDefaultTemplateId={second.Id}");
		archive.IsSuccessStatusCode.ShouldBeTrue(await archive.Content.ReadAsStringAsync());
		(await Client.GetFromJsonAsync<SalaryTemplateDto>($"{BaseUrl}/{first.Id}", JsonOptions))!.Status.ShouldBe("ARCHIVED");
		(await Client.GetFromJsonAsync<SalaryTemplateDto>($"{BaseUrl}/{second.Id}", JsonOptions))!.IsDefault.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_require_authentication_for_component_catalog()
	{
		using var anonymousClient = CreateAnonymousClient();
		(await anonymousClient.GetAsync($"{BaseUrl}/component-catalog")).StatusCode
			.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Salary_template_routes_should_enforce_the_permission_matrix()
	{
		using var deniedClient = CreateClientWithPermissions("__none__");
		using var viewClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View);
		var templateId = Guid.NewGuid();

		(await deniedClient.GetAsync($"{BaseUrl}/component-catalog")).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await deniedClient.PostAsJsonAsync($"{BaseUrl}/preview", new PreviewSalaryTemplateRequestDto())).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PostAsJsonAsync($"{BaseUrl}/preview", new PreviewSalaryTemplateRequestDto())).StatusCode
			.ShouldBe(HttpStatusCode.BadRequest);
		(await viewClient.GetAsync($"{BaseUrl}/component-catalog")).StatusCode
			.ShouldBe(HttpStatusCode.OK);
		(await viewClient.GetAsync($"{BaseUrl}/active")).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PostAsJsonAsync(
			$"{BaseUrl}/versions/{Guid.NewGuid()}/calculate",
			new CalculateSalaryTemplateRequestDto())).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PostAsJsonAsync(BaseUrl, new SaveSalaryTemplateDto())).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PutAsJsonAsync($"{BaseUrl}/{templateId}", new SaveSalaryTemplateDto())).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PostAsync($"{BaseUrl}/{templateId}/set-default", null)).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.DeleteAsync($"{BaseUrl}/{templateId}")).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public void Salary_template_actions_should_use_the_exact_permissions()
	{
		var expectedPolicies = new Dictionary<string, string>
		{
			[nameof(SalaryTemplateController.PreviewAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.GetListAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.GetActiveListAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage,
			[nameof(SalaryTemplateController.GetComponentCatalogAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.GetAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.GetVersionsAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.CreateAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add,
			[nameof(SalaryTemplateController.UpdateAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit,
			[nameof(SalaryTemplateController.SetDefaultAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.SetDefault,
			[nameof(SalaryTemplateController.ArchiveAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Archive,
			[nameof(SalaryTemplateController.CalculateAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage
		};

		foreach (var (methodName, expectedPolicy) in expectedPolicies)
		{
			var method = typeof(SalaryTemplateController).GetMethod(methodName);
			method.ShouldNotBeNull();
			method.GetCustomAttribute<AuthorizeAttribute>(inherit: true)
				.ShouldNotBeNull()
				.Policy.ShouldBe(expectedPolicy);
		}
	}

	private static SaveSalaryTemplateDto Save(string name, Guid basicComponentId) => new()
	{
		SampleMonthlyAmount = 20000m,
		SampleEffectiveMonth = new DateTime(2026, 9, 1),
		Name = name,
		Description = "Complete active salary template",
		DefaultSalaryBasis = "EMPLOYER_COST",
		DefaultProtectionPolicy = "PROTECT_EMPLOYER_COST",
		IdempotencyKey = Guid.NewGuid().ToString("N"),
		Rules =
		[
			new SalaryTemplateRuleDto
			{
				ComponentId = basicComponentId,
				LineKind = "EARNING",
				SemanticType = "BASIC",
				FormulaType = "PERCENTAGE_OF_GROSS",
				Value = 100m
			}
		]
	};

	private static PreviewSalaryTemplateRequestDto Preview(SaveSalaryTemplateDto input) => new()
	{
		Rules = input.Rules, SalaryBasis = input.DefaultSalaryBasis, ProtectionPolicy = input.DefaultProtectionPolicy,
		MonthlyAmount = input.SampleMonthlyAmount, EffectiveMonth = input.SampleEffectiveMonth
	};

	private HttpClient CreateClientWithPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", permissions));
		return client;
	}
}
