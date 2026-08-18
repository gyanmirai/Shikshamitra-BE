using System;
using System.Collections.Generic;
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
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_create_blank_salary_template_draft()
	{
		var response = await Client.PostAsJsonAsync(BaseUrl, new CreateSalaryTemplateDto
		{
			Name = $"Blank Composer {Guid.NewGuid():N}",
			Description = "Created through the flexible composer"
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.Created);
		var template = await response.Content.ReadFromJsonAsync<SalaryTemplateDto>(JsonOptions);
		template.ShouldNotBeNull();
		template.Status.ShouldBe("DRAFT");
		template.Latest.Rules.ShouldBeEmpty();
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
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_require_authentication_for_component_catalog()
	{
		using var anonymousClient = CreateAnonymousClient();

		var response = await anonymousClient.GetAsync($"{BaseUrl}/component-catalog");

		response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Salary_template_routes_should_enforce_the_permission_matrix()
	{
		using var deniedClient = CreateClientWithPermissions("__none__");
		using var viewClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View);
		var templateId = Guid.NewGuid();
		var versionId = Guid.NewGuid();

		(await deniedClient.GetAsync($"{BaseUrl}/component-catalog")).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.GetAsync($"{BaseUrl}/component-catalog")).StatusCode
			.ShouldBe(HttpStatusCode.OK);
		(await viewClient.PostAsJsonAsync(BaseUrl, new CreateSalaryTemplateDto
		{
			Name = "Denied create"
		})).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PutAsJsonAsync($"{BaseUrl}/{templateId}/versions/{versionId}",
			new UpdateSalaryTemplateDraftDto { Name = "Denied update" })).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PostAsync($"{BaseUrl}/{templateId}/versions/{versionId}/publish", null)).StatusCode
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
			[nameof(SalaryTemplateController.GetListAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.GetComponentCatalogAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.GetAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.View,
			[nameof(SalaryTemplateController.CreateAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add,
			[nameof(SalaryTemplateController.CreateNextVersionAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit,
			[nameof(SalaryTemplateController.UpdateDraftAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Edit,
			[nameof(SalaryTemplateController.PublishAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Publish,
			[nameof(SalaryTemplateController.CloneAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Add,
			[nameof(SalaryTemplateController.SetDefaultAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.SetDefault,
			[nameof(SalaryTemplateController.ArchiveAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsSalaryTemplates.Archive
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
