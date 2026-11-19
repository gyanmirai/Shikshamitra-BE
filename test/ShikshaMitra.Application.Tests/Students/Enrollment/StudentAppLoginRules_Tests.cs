using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("activation")]
	[InlineData("completion")]
	public Task App_login_optional_accounts_can_be_requested_after_completion_without_reopening_setup(string timing) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync(timing, emailEnabled: false, studentAccess: "optional", guardianAccess: "optional");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var item = await service.GetAsync(fixture.Case.Id);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed);
			item.StudentAccountRequired.ShouldBeFalse(); item.GuardianAccountRequired.ShouldBeFalse();
			item.CredentialsPending.ShouldBeFalse();
			(await service.GetPortalAccountsAsync(item.Id)).ShouldBeEmpty();
			var beforeSettings = (await service.GetSettingsAsync(item.AcademicYearId)).Single(value => value.AcademicYearId == item.AcademicYearId).ConfigurationJson;
			var workspace = await service.GetCredentialsAsync(item.Id);
			workspace.Recipients.ShouldAllBe(owner => owner.CanSend);
			workspace = await service.SendCredentialsAsync(item.Id, new() { AccountType = "student", Relationship = "self",
				ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			workspace = await service.SendCredentialsAsync(item.Id, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
				ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			item = await service.GetAsync(item.Id);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed); item.CredentialsPending.ShouldBeTrue();
			item.StudentAccountRequired.ShouldBeFalse(); item.GuardianAccountRequired.ShouldBeFalse();
			(await service.GetPortalAccountsAsync(item.Id)).Count.ShouldBe(2);
			var handoffs = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>().GetListAsync(value => value.OnboardingCaseId == item.Id));
			handoffs.Where(value => value.ServiceKey.EndsWith("-account")).ShouldAllBe(value => !value.Required);
			(await service.GetListAsync(new() { AcademicYearId = item.AcademicYearId, PendingTask = "credentials" })).Items.ShouldContain(value => value.Id == item.Id);
			(await service.GetSettingsAsync(item.AcademicYearId)).Single(value => value.AcademicYearId == item.AcademicYearId).ConfigurationJson.ShouldBe(beforeSettings);
		});

	[Fact]
	public Task App_login_not_needed_still_rejects_manual_account_creation() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false, studentAccess: "not-needed", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.ShouldAllBe(owner => !owner.CanSend);
		(await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId,
			new() { AccountType = "student", Relationship = "self", ExpectedEmail = fixture.StudentEmail,
				ConcurrencyStamp = workspace.ConcurrencyStamp }))).Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
	});

	[Theory]
	[InlineData("activation")]
	[InlineData("completion")]
	public Task App_login_automatic_optional_email_is_queued_without_blocking_completion(string timing) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync(timing, studentAccess: "optional", guardianAccess: "not-needed", provisioningMode: "automatic");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(fixture.Case.Id);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed);
		var links = await service.GetPortalAccountsAsync(item.Id);
		links.ShouldHaveSingleItem().AccountType.ShouldBe("student");
		var requests = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().GetListAsync(value => value.CaseId == item.Id));
		requests.ShouldHaveSingleItem().Status.ShouldBe("Pending");
		item.CredentialsPending.ShouldBeTrue();
	});

	[Fact]
	public Task App_login_automatic_parent_selection_waits_for_staff_when_multiple_parents_exist() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", studentAccess: "not-needed", guardianAccess: "required", provisioningMode: "automatic");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(fixture.Case.Id);
		item.Status.ShouldNotBe(StudentOnboardingStatus.Completed);
		(await service.GetPortalAccountsAsync(item.Id)).ShouldBeEmpty();
		var workspace = await service.GetCredentialsAsync(item.Id);
		workspace.Recipients.Single(value => value.Relationship == "father").CanSend.ShouldBeTrue();
		await service.SendCredentialsAsync(item.Id, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
			ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		(await service.GetPortalAccountsAsync(item.Id)).ShouldHaveSingleItem().Relationship.ShouldBe("father");
	});
}
