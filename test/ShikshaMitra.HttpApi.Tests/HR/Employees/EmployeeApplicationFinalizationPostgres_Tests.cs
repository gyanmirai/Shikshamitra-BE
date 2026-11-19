using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Models;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Employees;

[Collection(ApiTestCollection.Name)]
public class EmployeeApplicationFinalizationPostgres_Tests
{
	[PayrollPostgresFact]
	public async Task Finalization_saves_salary_atomically_retries_once_and_preserves_activation_and_editing_boundaries()
	{
		using var factory = new PayrollPostgresTestFactory();
		await new Scenario(factory).RunAsync();
	}

	private sealed class Scenario(PayrollPostgresTestFactory factory) : ShikshaMitraApiTestBase(factory)
	{
		public async Task RunAsync()
		{
			await PayrollTestHelper.EnsureActiveEsiTestSetupAsync(Factory);
			var invite = await PostAndReadAsync<EmployeeApplicationInviteDto>("/api/employee-applications", new CreateEmployeeApplicationInviteDto
			{
				FirstName = "Synthetic", LastName = "Salary", Email = $"salary-invite.{Guid.NewGuid():N}@test.com", MobileNo = "9876543210"
			});
			using var submission = EmployeeApplicationPublicApi_Tests.BuildMultipart(EmployeeApplicationPublicApi_Tests.ValidPayload("salary-onboarding"), includeValidFiles: true);
			var submitted = await Client.PostAsync($"/api/public/employee-applications/{invite.Token}/submit", submission);
			submitted.StatusCode.ShouldBe(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
			var templates = (await Client.GetFromJsonAsync<SalaryTemplateListDto>("/api/payroll/salary-templates/active", JsonOptions))!;
			var personal = EmployeeTestHelper.BuildFullPersonalInfoRequest("SalaryOnboarding");
			var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
			var input = new EmployeeApplicationFinalizeRequest
			{
				PersonalInfo = personal, AcademicYearId = Factory.SeededAcademicYearId,
				AcademicScopes = [new() { EducationBoardId = Factory.SeededBoardId, EducationMediumId = Factory.SeededMediumId, EducationMediumStageId = Factory.SeededMediumStageId }],
				StatutoryProfile = new PayrollStatutoryProfile { ApplyPf = false, PfDisabledReason = "Synthetic optional PF opt-out", ApplyEsic = false, EsicDisabledReason = "Synthetic external ESIC" },
				Salary = new SaveEmployeeCompensationDto
				{
					TemplateVersionId = templates.Items.First(x => x.IsDefault).EditableVersion.Id,
					SalaryBasis = "GROSS", MonthlyAmount = 50000m, EffectiveMonth = month, PfApplicable = false,
					PfEnrollment = new() { Membership = "NOT_MEMBER" }, EsiManagementMode = "EXTERNAL", EsiExternalReason = "Synthetic external ESIC",
					IdempotencyKey = "onboarding"
				}
			};
			var url = $"/api/employee-applications/{invite.Application.Id}/finalize";
			var salary = input.Salary;
			input.Salary = null!;
			(await Client.PostAsJsonAsync(url, input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			input.Salary = salary;
			input.Salary.MonthlyAmount = -1m;
			(await Client.PostAsJsonAsync(url, input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
			input.Salary.MonthlyAmount = 50000m;
			await AssertNoPartialStaffAsync(invite.Application.Id, personal.Email!);

			using var denied = Factory.CreateClient();
			denied.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, TestAuthHandler.TestTenantId);
			denied.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Finalize);
			(await denied.PostAsJsonAsync(url, input, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
			await AssertNoPartialStaffAsync(invite.Application.Id, personal.Email!);

			factory.Barrier.FailNextCommand("INSERT INTO \"HR_EmployeeCompensationRevisions\"");
			var failedSave = await Client.PostAsJsonAsync(url, input, JsonOptions);
			failedSave.IsSuccessStatusCode.ShouldBeFalse();
			await AssertNoPartialStaffAsync(invite.Application.Id, personal.Email!);

			var responses = await Task.WhenAll(Client.PostAsJsonAsync(url, input, JsonOptions), Client.PostAsJsonAsync(url, input, JsonOptions));
			foreach (var response in responses) response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			var first = (await responses[0].Content.ReadFromJsonAsync<EmployeeApplicationFinalizeResultDto>(JsonOptions))!;
			var replay = (await responses[1].Content.ReadFromJsonAsync<EmployeeApplicationFinalizeResultDto>(JsonOptions))!;
			first.Employee.Id.ShouldBe(replay.Employee.Id);
			first.Employee.EmployeeStatusType.ShouldBe(Common.EmployeeStatusType.Inactive);
			first.ActivationReadiness.CanActivate.ShouldBeTrue();
			var compensationUrl = $"/api/payroll/employees/{first.Employee.Id}/compensation";
			var history = (await Client.GetFromJsonAsync<CompensationRevisionListDto>(compensationUrl, JsonOptions))!;
			history.Items.Count.ShouldBe(1);
			history.Items[0].QuotedMonthlyAmount.ShouldBe(50000m);
			var profile = (await Client.GetFromJsonAsync<EmployeeStatutoryProfileDto>($"/api/employees/{first.Employee.Id}/payroll/statutory-profile", JsonOptions))!;
			profile.Profile.ApplyPf.ShouldBeFalse();
			profile.Profile.ApplyEsic.ShouldBeFalse();

			// The existing edit endpoint still creates a later revision without activating staff.
			input.Salary.ExpectedRevision = 1;
			input.Salary.IdempotencyKey = Guid.NewGuid().ToString("N");
			input.Salary.MonthlyAmount = 55000m;
			var edited = await Client.PutAsJsonAsync(compensationUrl, input.Salary, JsonOptions);
			edited.StatusCode.ShouldBe(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
			(await edited.Content.ReadFromJsonAsync<CompensationRevisionDto>(JsonOptions))!.RevisionNumber.ShouldBe(2);
			var finalReplay = await PostAndReadAsync<EmployeeApplicationFinalizeResultDto>(url, input);
			finalReplay.Employee.EmployeeStatusType.ShouldBe(Common.EmployeeStatusType.Inactive);
			(await Client.GetFromJsonAsync<CompensationRevisionListDto>(compensationUrl, JsonOptions))!.Items.Count.ShouldBe(2);
		}

		private async Task AssertNoPartialStaffAsync(Guid applicationId, string email)
		{
			(await Client.GetFromJsonAsync<EmployeeApplicationDto>($"/api/employee-applications/{applicationId}", JsonOptions))!.FinalizedEmployeeId.ShouldBeNull();
			using var scope = Factory.Services.CreateScope();
			using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId));
			using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true);
			(await scope.ServiceProvider.GetRequiredService<IEmployeeRepository>().GetListAsync(x => x.Email == email)).ShouldBeEmpty();
			await uow.CompleteAsync();
		}
	}
}
