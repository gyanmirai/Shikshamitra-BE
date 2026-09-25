using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public partial class CompensationRevisionApi_Tests : ShikshaMitraApiTestBase
{
	public CompensationRevisionApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Direct_save_should_be_idempotent_and_supersede_the_same_effective_month()
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "CompDirectSave");
		var template = await GetDefaultTemplateAsync(Client);
		var idempotencyKey = Guid.NewGuid().ToString("N");
		var firstRequest = Request(template, 50000m, idempotencyKey);

		var firstResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/compensation", firstRequest, JsonOptions);
		var firstPayload = await firstResponse.Content.ReadAsStringAsync();
		firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK, firstPayload);
		var first = await firstResponse.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions);
		first.ShouldNotBeNull();
		first.Status.ShouldBe("EFFECTIVE");
		first.RevisionNumber.ShouldBe(1);
		first.IsCurrent.ShouldBeTrue();
		first.ActivationReadiness.CanActivate.ShouldBeTrue();

		var replayResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/compensation", firstRequest, JsonOptions);
		replayResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var replay = await replayResponse.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions);
		replay.ShouldNotBeNull();
		replay.Id.ShouldBe(first.Id);

		var secondRequest = Request(template, 55000m, Guid.NewGuid().ToString("N"));
		secondRequest.ExpectedRevision = first.RevisionNumber;
		var secondResponse = await Client.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/compensation", secondRequest, JsonOptions);
		var secondPayload = await secondResponse.Content.ReadAsStringAsync();
		secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK, secondPayload);
		var second = await secondResponse.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions);
		second.ShouldNotBeNull();
		second.RevisionNumber.ShouldBe(2);
		second.Id.ShouldNotBe(first.Id);

		var history = await Client.GetFromJsonAsync<CompensationRevisionListDto>(
			$"/api/payroll/employees/{employeeId}/compensation", JsonOptions);
		history.ShouldNotBeNull();
		history.Items.Single(x => x.Id == first.Id).Status.ShouldBe("SUPERSEDED");
		history.Items.Single(x => x.Id == second.Id).IsCurrent.ShouldBeTrue();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Compensation_permissions_should_separate_manage_from_employee_edit()
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "CompPermissions");
		using var manageClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage);
		using var viewClient = CreateClientWithPermissions(
			ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View);
		using var deniedClient = CreateClientWithPermissions("__none__");
		var template = await GetDefaultTemplateAsync(manageClient);

		var saveResponse = await manageClient.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/compensation",
			Request(template, 50000m, Guid.NewGuid().ToString("N")), JsonOptions);
		var savePayload = await saveResponse.Content.ReadAsStringAsync();
		saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK, savePayload);
		(await manageClient.GetAsync($"/api/payroll/employees/{employeeId}/compensation"))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await viewClient.GetAsync($"/api/payroll/employees/{employeeId}/compensation"))
			.StatusCode.ShouldBe(HttpStatusCode.OK);
		(await viewClient.PutAsJsonAsync(
			$"/api/payroll/employees/{employeeId}/compensation",
			Request(template, 51000m, Guid.NewGuid().ToString("N")), JsonOptions))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await deniedClient.GetAsync($"/api/payroll/employees/{employeeId}/compensation"))
			.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task<SalaryTemplateDto> GetDefaultTemplateAsync(HttpClient client)
	{
		var templates = await client.GetFromJsonAsync<SalaryTemplateListDto>(
			"/api/payroll/salary-templates/active", JsonOptions);
		return templates!.Items.First(x => x.IsDefault);
	}

	private static SaveEmployeeCompensationDto Request(
		SalaryTemplateDto template,
		decimal amount,
		string idempotencyKey)
	{
		var now = DateTime.UtcNow;
		return new SaveEmployeeCompensationDto
		{
			TemplateVersionId = template.EditableVersion.Id,
			SalaryBasis = template.EditableVersion.DefaultSalaryBasis,
			ProtectionPolicy = template.EditableVersion.DefaultProtectionPolicy,
			MonthlyAmount = amount,
			EffectiveMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
			PfApplicable = template.EditableVersion.DefaultPfApplicable,
			PfRuleId = template.EditableVersion.DefaultPfRuleId,
			IdempotencyKey = idempotencyKey
		};
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
