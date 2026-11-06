using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public Task Staff_selected_login_prepares_only_selected_owners(bool student, bool parent) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("completion", accountsEnabled: false, emailEnabled: false);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		foreach (var type in new[] { "student", "guardian" }.Where(type => type == "student" ? student : parent))
		{
			var command = new SendStudentCredentialsDto { AccountType = type, Relationship = type == "student" ? "self" : "father",
				ExpectedEmail = type == "student" ? fixture.StudentEmail : fixture.FatherEmail, ConfirmRecipientAccess = type == "guardian", ConcurrencyStamp = workspace.ConcurrencyStamp };
			workspace = await service.SendCredentialsAsync(workspace.CaseId, command);
			await service.SendCredentialsAsync(workspace.CaseId, command); // A lost response cannot duplicate the intent.
		}
		var links = await service.GetPortalAccountsAsync(fixture.Case.Id);
		links.Count.ShouldBe((student ? 1 : 0) + (parent ? 1 : 0));
		links.Any(link => link.AccountType == "student").ShouldBe(student);
		links.Any(link => link.AccountType == "guardian").ShouldBe(parent);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == fixture.Case.Id))).ShouldBe(links.Count);
		(await service.GetAsync(fixture.Case.Id)).Status.ShouldBe(StudentOnboardingStatus.Completed);
	});
	[Fact]
	public Task Staff_selected_login_cancels_legacy_automatic_email_without_transport() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("completion", provisioningMode: "automatic");
		await LinkOneClickFixtureAccountsAsync(fixture);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var link = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>()
			.GetAsync(value => value.StudentId == fixture.Case.StudentId && value.AccountType == "student"));
		var request = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, fixture.Case.Id,
			fixture.Case.StudentId, link.Id, link.UserId, "student", fixture.StudentEmail, TestUserId, DateTime.UtcNow.AddMinutes(-3));
		var repository = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		await WithUnitOfWorkAsync(() => repository.InsertAsync(request, true));
		var sender = Substitute.For<IEmailSender>();
		var dispatcher = ActivatorUtilities.CreateInstance<StudentCredentialEmailDispatcher>(GetRequiredService<IServiceProvider>(), sender);
		await dispatcher.DispatchAsync(request.Id);
		(await WithUnitOfWorkAsync(() => repository.GetAsync(request.Id))).Status.ShouldBe("Cancelled");
		sender.ReceivedCalls().ShouldBeEmpty();
		(await service.GetAsync(fixture.Case.Id)).CredentialsPending.ShouldBeFalse();
	});
	[Theory]
	[InlineData("required", "automatic")]
	[InlineData("not-needed", "manual")]
	public Task Staff_selected_login_activates_without_accounts_and_ignores_old_school_policy(string access, string mode) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("completion", studentAccess: access, guardianAccess: access, provisioningMode: mode);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.GetAsync(fixture.Case.Id);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
		item.StudentAccountRequired.ShouldBeFalse(); item.GuardianAccountRequired.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(link => link.StudentId == item.StudentId))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(row => row.CaseId == item.Id))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordRegistrationReader>().GetSummaryAsync(item.StudentId, item.AcademicYearId))).Single().CredentialsStatus.ShouldBe("NotRequired");
		var renewal = System.Text.Json.JsonSerializer.Deserialize<StudentRecordRenewalRequirements>(await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordRenewalReadinessService>()
			.CaptureRulesAsync(item.StudentId, item.AcademicYearId, item.IntendedStandardId, item.DivisionId!.Value)))!;
		renewal.StudentAccount.ShouldBeFalse(); renewal.GuardianAccount.ShouldBeFalse();
		var workspace = await service.GetCredentialsAsync(item.Id);
		workspace.StudentAccessEnabled.ShouldBeTrue(); workspace.GuardianAccessEnabled.ShouldBeTrue();
		workspace.Recipients.Single(owner => owner.AccountType == "student").CanSend.ShouldBeTrue();
		workspace = await service.SendCredentialsAsync(item.Id, new() { AccountType = "student", Relationship = "self", ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		workspace.Recipients.Single(owner => owner.AccountType == "student").Delivery!.Status.ShouldBe("Pending");
		(await WithUnitOfWorkAsync(() => GetRequiredService<StudentRecordRegistrationReader>().GetSummaryAsync(item.StudentId, item.AcademicYearId))).Single().CredentialsStatus.ShouldBe("Pending");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(link => link.StudentId == item.StudentId && link.AccountType == "guardian"))).ShouldBe(0);
	});

	[Fact]
	public Task Staff_selected_login_legacy_required_handoffs_remain_history_without_blocking_setup() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", accountsEnabled: false);
		var repository = GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>();
		var handoff = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, fixture.Case.Id, "student-account", true, "retired-required-login");
		handoff.Fail("Old school account requirement", DateTime.UtcNow);
		await WithUnitOfWorkAsync(() => repository.InsertAsync(handoff, true));
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var item = await service.RefreshReadinessAsync(fixture.Case.Id, (await service.GetAsync(fixture.Case.Id)).ConcurrencyStamp);
		item.Status.ShouldBe(StudentOnboardingStatus.Completed);
		item.RequiredServiceHandoffs.ShouldBeEmpty(); item.CredentialsPending.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => repository.GetAsync(handoff.Id))).Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Failed);
	});
}
