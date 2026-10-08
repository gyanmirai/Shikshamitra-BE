using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class CompensationRevisionApi_Tests : ShikshaMitraApiTestBase
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
		first.EstimatedStatutoryDeductions.ShouldNotBeEmpty();
		(first.Lines.Where(x => x.IsEarning).Sum(x => x.Amount) - first.Lines.Where(x => !x.IsEarning).Sum(x => x.Amount) - first.EstimatedStatutoryDeductions.Sum(x => x.Amount)).ShouldBe(first.NetSalary);
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

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Inactive_employee_can_register_but_pending_registration_cannot_bypass_activation()
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "EsiOnboarding");
		var enrollmentUrl = $"/api/payroll/statutory-schemes/esi/employees/{employeeId}/enrollment";
		var pending = await Client.PutAsJsonAsync(enrollmentUrl, new UpdateEmployeeStatutoryEnrollmentDto
		{
			Status = "RegistrationPending", WageCeilingCategory = "Standard"
		}, JsonOptions);
		pending.StatusCode.ShouldBe(HttpStatusCode.OK, await pending.Content.ReadAsStringAsync());
		var request = Request(await GetDefaultTemplateAsync(Client), 10000m, Guid.NewGuid().ToString("N"));
		request.PfApplicable = false;
		request.PfRuleId = null;
		var salary = await Client.PutAsJsonAsync($"/api/payroll/employees/{employeeId}/compensation", request, JsonOptions);
		salary.StatusCode.ShouldBe(HttpStatusCode.OK, await salary.Content.ReadAsStringAsync());
		var readinessUrl = $"/api/employees/{employeeId}/activation-readiness";
		var readiness = (await Client.GetFromJsonAsync<EmployeeActivationReadinessDto>(readinessUrl, JsonOptions))!;
		readiness.CanActivate.ShouldBeFalse();
		readiness.BlockerCodes.ShouldContain(EsiStatutoryCalculator.InsuranceNumberIssue);
		var activation = new { employeeStatusType = 0, remarks = "Onboarding complete" };
		var denied = await Client.PutAsJsonAsync($"/api/employees/{employeeId}/status", activation, JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await denied.Content.ReadAsStringAsync());
		(await denied.Content.ReadAsStringAsync()).ShouldContain(EsiStatutoryCalculator.InsuranceNumberIssue);

		var registered = await Client.PutAsJsonAsync(enrollmentUrl, new UpdateEmployeeStatutoryEnrollmentDto
		{
			Status = "PendingReview", WageCeilingCategory = "Standard", InsuranceNumber = "1234567890"
		}, JsonOptions);
		registered.StatusCode.ShouldBe(HttpStatusCode.OK, await registered.Content.ReadAsStringAsync());
		(await Client.GetFromJsonAsync<EmployeeActivationReadinessDto>(readinessUrl, JsonOptions))!.CanActivate.ShouldBeTrue();
		var activated = await Client.PutAsJsonAsync($"/api/employees/{employeeId}/status", activation, JsonOptions);
		activated.StatusCode.ShouldBe(HttpStatusCode.OK, await activated.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("EMPLOYER_COST")]
	[InlineData("GROSS")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Bank_updates_should_round_trip_salary_basis_without_changing_compensation(string basis)
	{
		await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
		var employeeId = await EmployeeTestHelper.CreateEmployeeAndGetIdAsync(Client, "CompBankUpdate");
		var request = Request(await GetDefaultTemplateAsync(Client), 50000m, Guid.NewGuid().ToString("N"));
		request.SalaryBasis = basis;
		var compensationUrl = $"/api/payroll/employees/{employeeId}/compensation";
		var save = await Client.PutAsJsonAsync(compensationUrl, request, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var revision = (await save.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!;
		var payrollUrl = $"/api/employees/{employeeId}/payroll";
		var payroll = new PayrollContract
		{
			BankDetails = new BankDetailsContract
			{
				BankName = "State Bank of India", BranchName = "Main Branch",
				AccountHolderName = "CTC Test Employee", AccountNumber = "1234567890", IfscCode = "SBIN0001234"
			},
			SalaryDetails = new SalaryDetailsContract
			{
				QuotedSalaryBasis = basis, QuotedMonthlyAmount = revision.QuotedMonthlyAmount,
				SourceSalaryTemplateId = revision.SourceSalaryTemplateId,
				SourceSalaryTemplateVersionId = revision.SourceSalaryTemplateVersionId,
				SourceSalaryTemplateVersion = revision.SourceSalaryTemplateVersion,
				SalaryGeneratedAt = revision.SavedAt
			}
		};
		var update = await Client.PutAsJsonAsync(payrollUrl, payroll, JsonOptions);
		update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
		var read = (await Client.GetFromJsonAsync<EmployeePayrollResponse>(payrollUrl, JsonOptions))!;
		read.SalaryDetails.ShouldNotBeNull();
		read.SalaryDetails.QuotedSalaryBasis.ShouldBe(basis);
		read.SalaryDetails.SourceSalaryTemplateVersionId.ShouldBe(revision.SourceSalaryTemplateVersionId);
		read.SalaryDetails.QuotedMonthlyAmount.ShouldBe(50000m);

		payroll.BankDetails.AccountNumber = "9876543210";
		payroll.SalaryDetails = read.SalaryDetails;
		var second = await Client.PutAsJsonAsync(payrollUrl, payroll, JsonOptions);
		second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
		var reopened = (await Client.GetFromJsonAsync<EmployeePayrollResponse>(payrollUrl, JsonOptions))!;
		reopened.BankDetails.ShouldNotBeNull();
		reopened.SalaryDetails.ShouldNotBeNull();
		reopened.BankDetails.AccountNumber.ShouldBe("9876543210");
		reopened.SalaryDetails.QuotedSalaryBasis.ShouldBe(basis);
		var structureUrl = $"/api/payroll/employees/{employeeId}/salary-structure";
		var structureSave = await Client.PutAsJsonAsync(structureUrl, new UpdateSalaryStructureDto
		{
			QuotedSalaryBasis = basis, QuotedMonthlyAmount = revision.QuotedMonthlyAmount,
			SourceSalaryTemplateId = revision.SourceSalaryTemplateId,
			SourceSalaryTemplateVersionId = revision.SourceSalaryTemplateVersionId,
			SourceSalaryTemplateVersion = revision.SourceSalaryTemplateVersion,
			SalaryGeneratedAt = revision.SavedAt
		}, JsonOptions);
		structureSave.StatusCode.ShouldBe(HttpStatusCode.OK, await structureSave.Content.ReadAsStringAsync());
		var structure = (await Client.GetFromJsonAsync<SalaryStructureResponseDto>(structureUrl, JsonOptions))!;
		structure.QuotedSalaryBasis.ShouldBe(basis);
		structure.SourceSalaryTemplateVersionId.ShouldBe(revision.SourceSalaryTemplateVersionId);
		var history = (await Client.GetFromJsonAsync<CompensationRevisionListDto>(compensationUrl, JsonOptions))!;
		var unchanged = history.Items.ShouldHaveSingleItem();
		unchanged.Id.ShouldBe(revision.Id);
		unchanged.GrossSalary.ShouldBe(revision.GrossSalary);
		unchanged.NetSalary.ShouldBe(revision.NetSalary);
		unchanged.EmployerCost.ShouldBe(revision.EmployerCost);
		unchanged.Lines.Select(x => x.Amount).ShouldBe(revision.Lines.Select(x => x.Amount));
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
