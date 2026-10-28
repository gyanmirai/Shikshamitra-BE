using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("mother")]
	[InlineData("guardian")]
	public Task Onboarding_shared_adult_profile_mailbox_can_be_saved_without_granting_access(string relationship) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
		if (relationship == "guardian") await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Guardian", $"guardian-{Guid.NewGuid():N}@example.test")); await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == relationship);
		var admissionBefore = await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(fixture.Case.ApplicantId)).ExtraProperties));
		workspace = await service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, owner, fixture.FatherEmail));
		workspace.Recipients.Single(x => x.Relationship == relationship).Email.ShouldBe(fixture.FatherEmail);
		workspace.Recipients.Single(x => x.Relationship == "father").Email.ShouldBe(fixture.FatherEmail);
		workspace.Recipients.Single(x => x.Relationship == "self").Email.ShouldBe(fixture.StudentEmail);
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IdentityUserManager>().FindByEmailAsync(fixture.FatherEmail))).ShouldBeNull();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == fixture.Case.Id))).ShouldBe(0);
		(await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(fixture.Case.ApplicantId)).ExtraProperties))).ShouldBe(admissionBefore);
	});

	[Theory]
	[InlineData("self", "father")]
	[InlineData("father", "self")]
	public Task Onboarding_cross_account_profile_email_collision_remains_rejected(string relationship, string otherRelationship) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false); var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == relationship);
		var other = workspace.Recipients.Single(x => x.Relationship == otherRelationship);
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(fixture.Case.Id, EmailEdit(workspace, owner, other.Email!)));
		(await service.GetCredentialsAsync(fixture.Case.Id)).Recipients.Single(x => x.Relationship == relationship).Email.ShouldBe(owner.Email);
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
	});

	[Fact]
	public Task Onboarding_existing_parent_disabled_policy_hides_all_link_capabilities() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(parentAccess: "not-needed"); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "disabled-parent-capabilities" });
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.GuardianAccessEnabled.ShouldBeFalse(); workspace.ParentSibling!.Candidates.ShouldBeEmpty();
		workspace.ParentSibling.RequiresOnboardingConfirmation.ShouldBeFalse(); workspace.ParentSibling.CanLink.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(fixture.Case.Id,
			new() { ConfirmExistingParentAccess = true, ConcurrencyStamp = workspace.ConcurrencyStamp, SiblingParentSelection = ParentSelection(fixture, parent) }));
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
	});

	[Theory]
	[InlineData("required", StudentOnboardingStatus.NeedsAttention)]
	[InlineData("optional", StudentOnboardingStatus.Completed)]
	public Task Onboarding_unconfirmed_existing_parent_email_candidate_does_not_require_identity_update_for_activation(string access, StudentOnboardingStatus status) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(parentAccess: access); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		var service = CreateDeniedCredentialsService(IdentityPermissions.Users.Update);
		var activated = await WithUnitOfWorkAsync(() => service.ActivateAsync(fixture.Case.Id,
			new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "activate-without-parent-link" }));
		activated.StudentEnrollmentId.ShouldNotBeNull(); activated.Status.ShouldBe(status);
		(await GetRequiredService<IStudentOnboardingAppService>().GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
		var workspace = await GetRequiredService<IStudentOnboardingAppService>().GetCredentialsAsync(fixture.Case.Id);
		await WithUnitOfWorkAsync(() => Should.ThrowAsync<AbpAuthorizationException>(() => service.LinkParentAsync(fixture.Case.Id,
			new() { ConfirmExistingParentAccess = true, ConcurrencyStamp = workspace.ConcurrencyStamp, SiblingParentSelection = ParentSelection(fixture, parent) })));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == fixture.Case.Id))).ShouldBe(0);
	});

	[Theory]
	[InlineData("father", false)]
	[InlineData("mother", false)]
	[InlineData("guardian", false)]
	[InlineData("father", true)]
	public Task Onboarding_unconfirmed_existing_parent_email_can_be_corrected_before_granting_access(string relationship, bool legacyFlow) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, relationship == "guardian" ? "father" : relationship);
		if (legacyFlow) await WithUnitOfWorkAsync(async () => { var applicant = await _applicantRepository.GetAsync(fixture.ApplicantId); applicant.ExtraProperties.Remove("ParentAccountFlowVersion"); await _applicantRepository.UpdateAsync(applicant, true); });
		if (relationship == "guardian") await WithUnitOfWorkAsync(async () => { var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(fixture.Case.StudentId); family.UpdateGuardianDetails(ParentDetails("Saved Guardian", fixture.FatherEmail)); await families.UpdateAsync(family, true); });
		var admissionBefore = await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(fixture.ApplicantId)).ExtraProperties));
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "correct-unconfirmed-parent" });
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == relationship);
		workspace.ParentSibling!.RequiresOnboardingConfirmation.ShouldBeTrue(); workspace.ParentSibling.SelectedRelationship.ShouldBeNull();
		owner.CanEditEmail.ShouldBeTrue();
		var email = $"corrected-parent-{Guid.NewGuid():N}@example.test";
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, email));
		workspace.Recipients.Single(x => x.Relationship == relationship).Email.ShouldBe(email);
		workspace.ParentSibling!.Candidates.ShouldNotContain(x => x.Relationship == relationship);
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
		var sourceUser = await WithUnitOfWorkAsync(() => GetRequiredService<IdentityUserManager>().GetByIdAsync(parent.UserId));
		sourceUser.Email.ShouldBe(relationship == "mother" ? fixture.MotherEmail : fixture.FatherEmail);
		(await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(fixture.ApplicantId)).ExtraProperties))).ShouldBe(admissionBefore);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == fixture.Case.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Onboarding_existing_parent_help_cancels_when_original_source_profile_changes() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", withoutPassword: true);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "help-source-drift" });
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace = await service.LinkParentAsync(fixture.Case.Id, new() { ConcurrencyStamp = workspace.ConcurrencyStamp, ConfirmExistingParentAccess = true, SiblingParentSelection = ParentSelection(fixture, parent) });
		workspace = await service.SendCredentialsAsync(fixture.Case.Id, new() { AccountType = "guardian", Relationship = "father", ExpectedEmail = fixture.FatherEmail,
			ConcurrencyStamp = workspace.ConcurrencyStamp, ConfirmRecipientAccess = true, SendExistingParentHelp = true });
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>(); var row = await WithUnitOfWorkAsync(() => requests.GetAsync(x => x.CaseId == fixture.Case.Id));
		await WithUnitOfWorkAsync(async () => { var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(parent.StudentId); family.UpdateFatherDetails(ParentDetails("Changed Source", "changed@example.test")); await families.UpdateAsync(family, true); });
		var sender = Substitute.For<IEmailSender>(); var dispatcher = ActivatorUtilities.CreateInstance<StudentCredentialEmailDispatcher>(GetRequiredService<IServiceProvider>(), sender);
		await dispatcher.DispatchAsync(row.Id);
		(await WithUnitOfWorkAsync(() => requests.GetAsync(row.Id))).Status.ShouldBe("Cancelled"); sender.ReceivedCalls().ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IdentityUserManager>().GetByIdAsync(parent.UserId))).PasswordHash.ShouldBeNull();
		await WithUnitOfWorkAsync(async () => { var cancelled = await requests.GetAsync(row.Id); typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.RequestedAt))!.SetValue(cancelled, DateTime.UtcNow.AddMinutes(-4)); await requests.UpdateAsync(cancelled, true); });
		var refreshed = await service.GetCredentialsAsync(fixture.Case.Id);
		refreshed.Recipients.Single(x => x.Relationship == "father").CanSend.ShouldBeFalse();
		refreshed.Recipients.Single(x => x.Relationship == "father").CanSendExistingParentHelp.ShouldBeFalse();
	});

	[Fact]
	public Task Onboarding_existing_parent_link_rechecks_stamp_scope_tenant_permission_and_exact_saved_relationship() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var before = await service.GetCredentialsAsync(fixture.Case.Id);
		var input = new LinkStudentSiblingParentDto { ConfirmExistingParentAccess = true, ConcurrencyStamp = before.ConcurrencyStamp, SiblingParentSelection = ParentSelection(fixture, parent) };
		await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(fixture.Case.Id, input));
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = before.ConcurrencyStamp, IdempotencyKey = "exact-parent" });
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); input.ConcurrencyStamp = "stale";
		(await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(fixture.Case.Id, input))).Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
		input.ConcurrencyStamp = workspace.ConcurrencyStamp;
		using (_currentTenant.Change(Guid.NewGuid())) await Should.ThrowAsync<EntityNotFoundException>(() => service.LinkParentAsync(fixture.Case.Id, input));
		await WithUnitOfWorkAsync(() => Should.ThrowAsync<AbpAuthorizationException>(() => CreateDeniedCredentialsService(IdentityPermissions.Users.Update).LinkParentAsync(fixture.Case.Id, input)));
		await WithUnitOfWorkAsync(() => Should.ThrowAsync<AbpAuthorizationException>(() => CreateDeniedCredentialsService(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit).LinkParentAsync(fixture.Case.Id, input)));
		input.SiblingParentSelection!.ExpectedUserId = Guid.NewGuid(); await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(fixture.Case.Id, input));
		input.SiblingParentSelection = ParentSelection(fixture, parent);
		var staffId = Guid.NewGuid(); await EnsureTestIdentityUserAsync(staffId, $"own-parent-scope-{staffId:N}");
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: staffId,
			academicYearId: fixture.Case.AcademicYearId, educationBoardId: fixture.Offering.BoardId, educationMediumId: fixture.Offering.MediumId,
			educationMediumStageId: fixture.Offering.StageId, standardId: fixture.Offering.StandardId, divisionId: fixture.Case.DivisionId), true));
		using (_principalAccessor.Change(EligibilityPrincipal(staffId, "own-parent-scope", "staff")))
		{
			(await service.GetCredentialsAsync(fixture.Case.Id)).ParentSibling!.Candidates.ShouldBeEmpty();
			await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(fixture.Case.Id, input));
		}
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
	});

	[Fact]
	public Task Onboarding_existing_parent_unconfirmed_identity_requires_exact_source_initial_setup_evidence() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(); var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", confirmed: false);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		(await service.GetCredentialsAsync(fixture.Case.Id)).ParentSibling!.Candidates.ShouldBeEmpty();
		var rows = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var row = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), fixture.Case.StudentId, parent.LinkId, parent.UserId, "guardian", fixture.FatherEmail, TestUserId, DateTime.UtcNow);
		await WithUnitOfWorkAsync(() => rows.InsertAsync(row, true));
		(await service.GetCredentialsAsync(fixture.Case.Id)).ParentSibling!.Candidates.ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () => { var saved = await rows.GetAsync(row.Id); typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.StudentId))!.SetValue(saved, parent.StudentId); await rows.UpdateAsync(saved, true); });
		(await service.GetCredentialsAsync(fixture.Case.Id)).ParentSibling!.Candidates.ShouldHaveSingleItem().UserId.ShouldBe(parent.UserId);
	});

	[Fact]
	public Task Onboarding_profile_email_collision_rejects_another_recipient_and_staff_identity_without_saving() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation"); var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id); var owner = workspace.Recipients.Single(x => x.Relationship == "father");
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, fixture.StudentEmail)));
		var staffEmail = $"staff-collision-{Guid.NewGuid():N}@example.test"; var manager = GetRequiredService<IdentityUserManager>();
		await WithUnitOfWorkAsync(async () => { (await manager.CreateAsync(new IdentityUser(Guid.NewGuid(), staffEmail, staffEmail, TestTenantId))).Succeeded.ShouldBeTrue(); });
		await Should.ThrowAsync<BusinessException>(() => service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, staffEmail)));
		(await service.GetCredentialsAsync(workspace.CaseId)).Recipients.Single(x => x.Relationship == "father").Email.ShouldBe(fixture.FatherEmail);
	});

	[Theory]
	[InlineData("inactive")]
	[InlineData("mixed-role")]
	[InlineData("unverified-link")]
	[InlineData("source-email")]
	public Task Onboarding_existing_parent_candidates_reject_ineligible_accounts_without_exposing_identity(string mode) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "ineligible-parent" });
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var user = await manager.GetByIdAsync(parent.UserId);
			if (mode == "inactive") { user.SetIsActive(false); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue(); }
			if (mode == "mixed-role")
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync("Student")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Student", TestTenantId))).Succeeded.ShouldBeTrue();
				(await manager.AddToRoleAsync(user, "Student")).Succeeded.ShouldBeTrue();
			}
			if (mode == "unverified-link")
			{
				var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>(); var link = await links.GetAsync(parent.LinkId);
				typeof(StudentPortalAccountLink).GetProperty(nameof(StudentPortalAccountLink.VerifiedByUserId))!.SetValue(link, Guid.Empty); await links.UpdateAsync(link, true);
			}
			if (mode == "source-email")
			{
				var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(parent.StudentId);
				family.UpdateFatherDetails(ParentDetails("Other", "other@example.test")); await families.UpdateAsync(family, true);
			}
		});
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.ParentSibling!.Candidates.ShouldBeEmpty();
		var input = JsonSerializer.Deserialize<LinkStudentSiblingParentDto>(JsonSerializer.Serialize(new { workspace.ConcurrencyStamp, SiblingParentSelection = ParentSelection(fixture, parent), ConfirmExistingParentAccess = true }))!;
		await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(fixture.Case.Id, input));
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldBeEmpty();
	});

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Onboarding_existing_parent_explicit_help_never_creates_or_changes_password(bool hasPassword) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", withoutPassword: !hasPassword);
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "parent-help" });
		var workspace = await service.GetCredentialsAsync(activated.Id);
		var manager = GetRequiredService<IdentityUserManager>();
		var original = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash;
		var input = JsonSerializer.Deserialize<SendStudentCredentialsDto>(JsonSerializer.Serialize(new { AccountType = "guardian", Relationship = "father", ExpectedEmail = fixture.FatherEmail,
			workspace.ConcurrencyStamp, ConfirmRecipientAccess = true, SendExistingParentHelp = true }))!;
		workspace = await service.SendCredentialsAsync(activated.Id, input);
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		await service.SendCredentialsAsync(activated.Id, input);
		(await WithUnitOfWorkAsync(() => requests.CountAsync(x => x.CaseId == activated.Id))).ShouldBe(1);
		var request = await WithUnitOfWorkAsync(() => requests.GetAsync(x => x.CaseId == activated.Id));
		request.RequiresSavedOwnerValidation().ShouldBeTrue();
		var sender = Substitute.For<IEmailSender>();
		var provider = GetRequiredService<IServiceProvider>();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["App:ClientUrl"] = "https://school.example.test" }).Build();
		var resolver = ActivatorUtilities.CreateInstance<StudentOnboardingCredentialEligibilityResolver>(provider, configuration);
		var dispatcher = ActivatorUtilities.CreateInstance<StudentCredentialEmailDispatcher>(provider, sender, resolver);
		await dispatcher.DispatchAsync(request.Id);
		(await WithUnitOfWorkAsync(() => requests.GetAsync(request.Id))).Status.ShouldBe("Sent");
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(original);
		var body = sender.ReceivedCalls().Single(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).GetArguments()[2]!.ToString()!;
		body.ShouldContain("reset-password"); body.ShouldNotContain("Temporary password");
		workspace = await service.GetCredentialsAsync(activated.Id); input.ConcurrencyStamp = workspace.ConcurrencyStamp;
		await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(activated.Id, input));
	});

	[Theory]
	[InlineData("father", false)]
	[InlineData("mother", false)]
	[InlineData("guardian", false)]
	[InlineData("father", true)]
	public Task Onboarding_existing_parent_requires_own_confirmation_without_admission_family_proof(string relationship, bool legacyFlow) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, relationship == "guardian" ? "father" : relationship);
		if (legacyFlow) await WithUnitOfWorkAsync(async () => { var applicant = await _applicantRepository.GetAsync(fixture.ApplicantId); applicant.ExtraProperties.Remove("ParentAccountFlowVersion"); await _applicantRepository.UpdateAsync(applicant, true); });
		if (relationship == "guardian") await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Guardian", fixture.FatherEmail)); await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "own-parent-confirm" });
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.FamilyConfirmed.ShouldBeFalse();
		workspace.ParentSibling.Candidates.Single(x => x.Relationship == relationship).UserId.ShouldBe(parent.UserId);
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldBeEmpty();
		var input = new LinkStudentSiblingParentDto { ConcurrencyStamp = workspace.ConcurrencyStamp, SiblingParentSelection = ParentSelection(fixture, parent) };
		input.SiblingParentSelection.Relationship = relationship;
		await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(activated.Id, input));
		var applicantBefore = await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(fixture.ApplicantId)).ExtraProperties));
		input = JsonSerializer.Deserialize<LinkStudentSiblingParentDto>(JsonSerializer.Serialize(new { input.ConcurrencyStamp, input.SiblingParentSelection, ConfirmExistingParentAccess = true }))!;
		workspace = await service.LinkParentAsync(activated.Id, input);
		workspace.ParentSibling!.IsLinked.ShouldBeTrue();
		(await service.GetAsync(activated.Id)).Status.ShouldBe(StudentOnboardingStatus.Completed);
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem().UserId.ShouldBe(parent.UserId);
		(await WithUnitOfWorkAsync(async () => JsonSerializer.Serialize((await _applicantRepository.GetAsync(fixture.ApplicantId)).ExtraProperties))).ShouldBe(applicantBefore);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == activated.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Onboarding_saved_guardian_email_can_be_added_without_granting_access() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Guardian", string.Empty));
			await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var owner = workspace.Recipients.Single(x => x.Relationship == "guardian");
		owner.CanEditEmail.ShouldBeTrue();
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, "own-guardian@example.test"));
		workspace.Recipients.Single(x => x.Relationship == "guardian").Email.ShouldBe("own-guardian@example.test");
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
	});

	[Fact]
	public Task Onboarding_linked_guardian_email_requires_owner_verification_and_preserves_password() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false);
		var email = $"guardian-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Guardian", email)); await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace = await service.SendCredentialsAsync(workspace.CaseId, new() { AccountType = "guardian", Relationship = "guardian", ExpectedEmail = email,
			ConfirmRecipientAccess = true, ConcurrencyStamp = workspace.ConcurrencyStamp });
		var link = (await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldHaveSingleItem();
		var manager = GetRequiredService<IdentityUserManager>();
		await WithUnitOfWorkAsync(async () => { var user = await manager.GetByIdAsync(link.UserId); (await manager.AddPasswordAsync(user, "Guardian-Owner29!")).Succeeded.ShouldBeTrue(); });
		var hash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId))).PasswordHash;
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		var owner = workspace.Recipients.Single(x => x.Relationship == "guardian"); owner.CanEditEmail.ShouldBeTrue();
		var nextEmail = $"guardian-new-{Guid.NewGuid():N}@example.test";
		workspace = await service.UpdateCredentialsEmailAsync(workspace.CaseId, EmailEdit(workspace, owner, nextEmail));
		workspace.Recipients.Single(x => x.Relationship == "guardian").Email.ShouldBe(email);
		await ConfirmEmailForTestAsync(link.UserId, nextEmail);
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		workspace.Recipients.Single(x => x.Relationship == "guardian").Email.ShouldBe(nextEmail);
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(link.UserId))).PasswordHash.ShouldBe(hash);
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldHaveSingleItem().UserId.ShouldBe(link.UserId);
	});
}
