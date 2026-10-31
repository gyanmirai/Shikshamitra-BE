using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public void Student_app_access_correction_requires_existing_configuration_authority()
	{
		var method = typeof(StudentOnboardingAppService).GetMethod("EnableStudentAppAccessAsync");
		method.ShouldNotBeNull("Completed cases need an audited, scoped Student access correction command.");
		method.GetCustomAttributes<AuthorizeAttribute>().Select(value => value.Policy)
			.ShouldContain(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure);
		typeof(IStudentOnboardingAppService).GetMethod(method.Name).ShouldNotBeNull();
	}

	[Theory]
	[InlineData("activation")]
	[InlineData("completion")]
	public Task Student_app_access_correction_preserves_completed_case_and_all_other_rules_before_separate_send(string timing) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateOneClickCredentialsCaseAsync(timing, emailEnabled: false, studentAccess: "not-needed", guardianAccess: "optional");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var cases = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			var events = GetRequiredService<IRepository<StudentOnboardingEvent, Guid>>();
			var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			workspace.StudentAccessEnabled.ShouldBeFalse(); workspace.CanEnableStudentAccess.ShouldBeTrue();
			fixture.Case.Status.ShouldBe(StudentOnboardingStatus.Completed);
			var beforeRules = await WithUnitOfWorkAsync(async () => (string)(await cases.GetAsync(workspace.CaseId)).ExtraProperties["ReadinessRulesSnapshot"].ShouldNotBeNull());
			var beforeSettings = (await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson;
			var beforeHandoffs = (await service.GetServiceHandoffsAsync(workspace.CaseId)).Select(value => value.Id).Order().ToArray();
			workspace = await service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Student requested app access", ConcurrencyStamp = workspace.ConcurrencyStamp });
			workspace.StudentAccessEnabled.ShouldBeTrue(); workspace.CanEnableStudentAccess.ShouldBeFalse();
			workspace.Recipients.Single(value => value.AccountType == "student").CanSend.ShouldBeTrue();
			var expected = JsonNode.Parse(beforeRules)!;
			expected["studentAccountAccess"] = "optional"; expected["studentAccount"] = false;
			await WithUnitOfWorkAsync(async () =>
			{
				var item = await cases.GetAsync(workspace.CaseId);
				JsonNode.DeepEquals(expected, JsonNode.Parse((string)item.ExtraProperties["ReadinessRulesSnapshot"].ShouldNotBeNull())).ShouldBeTrue();
				item.Status.ShouldBe(StudentOnboardingStatus.Completed);
				var audit = (await events.GetListAsync(value => value.OnboardingCaseId == item.Id && value.EventType == "StudentAppAccessEnabled")).ShouldHaveSingleItem();
				audit.CreatorId.ShouldBe(TestUserId); audit.Description.ShouldContain("Student requested app access");
				audit.MetadataJson.ShouldNotBeNull().ShouldContain("Previous"); audit.MetadataJson.ShouldNotBeNull().ShouldContain("optional");
				(await requests.GetListAsync(value => value.CaseId == item.Id)).ShouldBeEmpty();
			});
			(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
			(await service.GetServiceHandoffsAsync(workspace.CaseId)).Select(value => value.Id).Order().ShouldBe(beforeHandoffs);
			(await service.GetSettingsAsync(fixture.Case.AcademicYearId)).Single(value => value.AcademicYearId == fixture.Case.AcademicYearId).ConfigurationJson.ShouldBe(beforeSettings);
			var stamp = workspace.ConcurrencyStamp;
			workspace = await service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Repeated review", ConcurrencyStamp = stamp });
			workspace.ConcurrencyStamp.ShouldBe(stamp);
			(await WithUnitOfWorkAsync(() => events.CountAsync(value => value.OnboardingCaseId == workspace.CaseId && value.EventType == "StudentAppAccessEnabled"))).ShouldBe(1);
			await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "student", Relationship = "self", ExpectedEmail = fixture.StudentEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
			(await service.GetAsync(workspace.CaseId)).Status.ShouldBe(StudentOnboardingStatus.Completed);
			(await service.GetServiceHandoffsAsync(workspace.CaseId)).Single(value => value.ServiceKey == "student-account").Required.ShouldBeFalse();
			(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldHaveSingleItem().AccountType.ShouldBe("student");
			(await WithUnitOfWorkAsync(() => requests.GetListAsync(value => value.CaseId == workspace.CaseId))).ShouldHaveSingleItem().Status.ShouldBe("Pending");
		});

	[Theory]
	[InlineData("optional")]
	[InlineData("required")]
	public Task Student_app_access_correction_never_downgrades_existing_access(string access) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false, studentAccess: access, guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var repository = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var before = await WithUnitOfWorkAsync(async () => (string)(await repository.GetAsync(fixture.Case.Id)).ExtraProperties["ReadinessRulesSnapshot"].ShouldNotBeNull());
		var result = await service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Confirm existing access", ConcurrencyStamp = workspace.ConcurrencyStamp });
		result.StudentAccessEnabled.ShouldBeTrue(); result.CanEnableStudentAccess.ShouldBeFalse();
		result.ConcurrencyStamp.ShouldBe(workspace.ConcurrencyStamp);
		(await WithUnitOfWorkAsync(async () => (string)(await repository.GetAsync(fixture.Case.Id)).ExtraProperties["ReadinessRulesSnapshot"].ShouldNotBeNull())).ShouldBe(before);
		(await service.GetAsync(fixture.Case.Id)).Status.ShouldBe(fixture.Case.Status);
	});

	[Fact]
	public Task Student_app_access_correction_preserves_existing_parent_identity_links_and_delivery() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false, studentAccess: "not-needed", guardianAccess: "optional");
		var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "guardian", Relationship = "father", ConfirmRecipientAccess = true, ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		var parent = (await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldHaveSingleItem();
		var users = GetRequiredService<IdentityUserManager>();
		var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var identityBefore = await WithUnitOfWorkAsync(async () => { var user = (await users.FindByIdAsync(parent.UserId.ToString())).ShouldNotBeNull(); return (user.Email, user.PasswordHash, user.ConcurrencyStamp); });
		var linksBefore = await WithUnitOfWorkAsync(async () => (await links.GetListAsync(value => value.UserId == parent.UserId)).Select(value => value.Id).Order().ToArray());
		var requestBefore = await WithUnitOfWorkAsync(() => requests.GetAsync(value => value.CaseId == workspace.CaseId));
		await service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Student requested separate login", ConcurrencyStamp = workspace.ConcurrencyStamp });
		(await WithUnitOfWorkAsync(async () => { var user = (await users.FindByIdAsync(parent.UserId.ToString())).ShouldNotBeNull(); return (user.Email, user.PasswordHash, user.ConcurrencyStamp); })).ShouldBe(identityBefore);
		(await WithUnitOfWorkAsync(async () => (await links.GetListAsync(value => value.UserId == parent.UserId)).Select(value => value.Id).Order().ToArray())).ShouldBe(linksBefore);
		var requestAfter = await WithUnitOfWorkAsync(() => requests.GetAsync(value => value.CaseId == workspace.CaseId));
		requestAfter.Id.ShouldBe(requestBefore.Id); requestAfter.Email.ShouldBe(requestBefore.Email); requestAfter.Status.ShouldBe(requestBefore.Status);
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldHaveSingleItem().UserId.ShouldBe(parent.UserId);
	});

	[Fact]
	public Task Student_app_access_correction_rejects_stale_invalid_foreign_and_unassigned_requests() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", studentAccess: "not-needed", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>(); var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		await Should.ThrowAsync<AbpValidationException>(() => service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "", ConcurrencyStamp = workspace.ConcurrencyStamp }));
		// Direct calls bypass ABP's validation interceptor; the command must still reject a whitespace-only reason.
		var direct = CreateDeniedCredentialsService(IdentityPermissions.Users.Create);
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => direct.EnableStudentAppAccessAsync(workspace.CaseId,
			new() { Reason = "   ", ConcurrencyStamp = workspace.ConcurrencyStamp })))).Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
		(await Should.ThrowAsync<BusinessException>(() => service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Student access", ConcurrencyStamp = "stale" }))).Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Student access", ConcurrencyStamp = workspace.ConcurrencyStamp }));
		using (_principalAccessor.Change(EligibilityPrincipal(Guid.NewGuid(), "student-access-no-scope", "staff")))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Student access", ConcurrencyStamp = workspace.ConcurrencyStamp }));
		var denied = CreateDeniedCredentialsService(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Configure);
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => denied.EnableStudentAppAccessAsync(workspace.CaseId, new() { Reason = "Student access", ConcurrencyStamp = workspace.ConcurrencyStamp })));
		(await service.GetCredentialsAsync(workspace.CaseId)).StudentAccessEnabled.ShouldBeFalse();
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
	});

	[Theory]
	[InlineData("missing-snapshot")]
	[InlineData("invalid-snapshot")]
	[InlineData("inactive-enrollment")]
	[InlineData("missing-enrollment")]
	[InlineData("cancelled")]
	public Task Student_app_access_correction_rejects_unreviewed_or_inactive_case(string condition) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", studentAccess: "not-needed", guardianAccess: "not-needed");
		var service = GetRequiredService<IStudentOnboardingAppService>(); var repository = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
		var stamp = await WithUnitOfWorkAsync(async () =>
		{
			var item = await repository.GetAsync(fixture.Case.Id);
			if (condition == "missing-snapshot") item.ExtraProperties.Remove("ReadinessRulesSnapshot");
			if (condition == "invalid-snapshot") item.ExtraProperties["ReadinessRulesSnapshot"] = "[]";
			if (condition == "inactive-enrollment")
			{
				var enrollments = GetRequiredService<IRepository<StudentEnrollment, Guid>>();
				var enrollment = await enrollments.GetAsync(item.StudentEnrollmentId!.Value); enrollment.Deactivate(); await enrollments.UpdateAsync(enrollment, true);
			}
			if (condition == "missing-enrollment") typeof(StudentOnboardingCase).GetProperty(nameof(StudentOnboardingCase.StudentEnrollmentId))!.SetValue(item, null);
			if (condition == "cancelled") typeof(StudentOnboardingCase).GetProperty(nameof(StudentOnboardingCase.Status))!.SetValue(item, StudentOnboardingStatus.Cancelled);
			await repository.UpdateAsync(item, true); return item.ConcurrencyStamp;
		});
		(await Should.ThrowAsync<BusinessException>(() => service.EnableStudentAppAccessAsync(fixture.Case.Id, new() { Reason = "Student access", ConcurrencyStamp = stamp }))).Code
			.ShouldBe(condition.EndsWith("snapshot") ? StudentOnboardingErrorCodes.PortalAccountInvalid : StudentOnboardingErrorCodes.CaseNotEditable);
	});
}
