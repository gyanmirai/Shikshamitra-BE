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
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class EsiStatutoryApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/payroll/statutory-schemes/esi";

	public EsiStatutoryApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_return_effective_gujarat_esi_rule()
	{
		var response = await Client.GetAsync($"{BaseUrl}/rule?effectiveOn=2026-08-01");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var rule = await response.Content.ReadFromJsonAsync<StatutoryRuleVersionDto>(JsonOptions);
		rule.ShouldNotBeNull();
		rule.SchemeCode.ShouldBe("ESI");
		rule.JurisdictionCode.ShouldBe("IN-GJ");
		rule.StandardWageCeiling.ShouldBe(21_000m);
		rule.DisabilityWageCeiling.ShouldBe(25_000m);
		rule.EmployeePercentage.ShouldBe(0.75m);
		rule.EmployerPercentage.ShouldBe(3.25m);
	}

	[Fact]
	public async Task Should_require_authentication()
	{
		using var anonymousClient = CreateAnonymousClient();

		(await anonymousClient.GetAsync($"{BaseUrl}/rule")).StatusCode
			.ShouldBe(HttpStatusCode.Unauthorized);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Routes_should_enforce_statutory_permission_matrix()
	{
		using var deniedClient = CreateClientWithPermissions("__none__");
		using var viewClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View);
		var employeeId = Guid.NewGuid();

		(await deniedClient.GetAsync($"{BaseUrl}/rule")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.GetAsync($"{BaseUrl}/rule")).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await viewClient.PutAsJsonAsync($"{BaseUrl}/establishment", new ConfigureStatutoryEstablishmentDto()))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PutAsJsonAsync($"{BaseUrl}/employees/{employeeId}/enrollment",
			new UpdateEmployeeStatutoryEnrollmentDto())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await viewClient.PostAsJsonAsync($"{BaseUrl}/activate",
			new StatutoryImpactPreviewRequestDto { MonthYear = "08-2026" })).StatusCode
			.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public void Actions_should_use_exact_permissions()
	{
		var expectedPolicies = new Dictionary<string, string>
		{
			[nameof(EsiStatutoryController.GetRuleAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View,
			[nameof(EsiStatutoryController.ConfigureEstablishmentAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Edit,
			[nameof(EsiStatutoryController.UpdateComponentTreatmentsAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Edit,
			[nameof(EsiStatutoryController.UpdateEnrollmentAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ManageEnrollment,
			[nameof(EsiStatutoryController.CreateOverrideAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.ApproveOverride,
			[nameof(EsiStatutoryController.GetOverridesAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.View,
			[nameof(EsiStatutoryController.ActivateAsync)] =
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsStatutorySchemes.Activate
		};

		foreach (var (methodName, expectedPolicy) in expectedPolicies)
		{
			var method = typeof(EsiStatutoryController).GetMethod(methodName);
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
