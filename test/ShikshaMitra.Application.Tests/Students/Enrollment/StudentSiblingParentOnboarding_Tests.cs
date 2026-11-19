using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
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
	[Fact]
	public Task Sibling_parent_onboarding_rejects_deleted_confirmation_source_before_activation() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		await WithUnitOfWorkAsync(() => _applicantRepository.DeleteAsync(parent.ApplicantId, true));
		var service = GetRequiredService<StudentSiblingParentService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			var context = await service.GetAsync(item);
			context.Status.ShouldBe("ReviewRequired"); context.BlockedReason.ShouldNotBeNull();
			(await Should.ThrowAsync<BusinessException>(() => service.PrepareSelectionAsync(item, null))).Code
				.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
		});
	});

	[Fact]
	public Task Sibling_parent_onboarding_rejects_an_unverified_target_link_even_when_parent_candidate_is_valid() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		await WithUnitOfWorkAsync(async () =>
		{
			var link = new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, fixture.Case.StudentId,
				parent.UserId, "guardian", "father", TestUserId);
			// Represent a legacy/imported link without the required staff-verification provenance.
			typeof(StudentPortalAccountLink).GetProperty(nameof(StudentPortalAccountLink.VerifiedByUserId))!.SetValue(link, Guid.Empty);
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(link, true);
			var service = GetRequiredService<StudentSiblingParentService>(); var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			var context = await service.GetAsync(item);
			context.IsLinked.ShouldBeFalse(); context.Status.ShouldBe("ReviewRequired");
			await Should.ThrowAsync<BusinessException>(() => service.PrepareSelectionAsync(item, null));
		});
	});

	[Theory]
	[InlineData("Pending")]
	[InlineData("Processing")]
	[InlineData("Sent")]
	[InlineData("Failed")]
	public Task Sibling_parent_onboarding_accepts_only_the_exact_verified_childs_initial_setup_request(string status) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", confirmed: false);
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var repository = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var request = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), fixture.Case.StudentId,
			parent.LinkId, parent.UserId, "guardian", fixture.FatherEmail, TestUserId, DateTime.UtcNow.AddMinutes(-4));
		if (status != "Pending") request.Claim(DateTime.UtcNow.AddMinutes(-3));
		if (status == "Sent") request.Sent(DateTime.UtcNow.AddMinutes(-3));
		if (status == "Failed") request.Failed(DateTime.UtcNow.AddMinutes(-3));
		await WithUnitOfWorkAsync(() => repository.InsertAsync(request, true));
		var service = GetRequiredService<StudentSiblingParentService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			(await service.GetAsync(item)).Candidates.ShouldBeEmpty();
			var saved = await repository.GetAsync(request.Id);
			typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.StudentId))!.SetValue(saved, parent.StudentId);
			await repository.UpdateAsync(saved, true);
			var candidate = (await service.GetAsync(item)).Candidates.ShouldHaveSingleItem();
			candidate.UserId.ShouldBe(parent.UserId);
			candidate.SetupStatus.ShouldBe(status == "Failed" ? "Failed" : "Pending");
		});
	});

	[Fact]
	public Task Sibling_parent_onboarding_requires_explicit_choice_and_rejects_saved_selection_drift() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var father = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		var mother = await AddSiblingParentOnboardingSourceAsync(fixture, "mother");
		await ConfirmSiblingParentOnboardingAsync(fixture, father);
		var service = GetRequiredService<StudentSiblingParentService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			var context = await service.GetAsync(item);
			context.RequiresSelection.ShouldBeTrue(); context.Candidates.Count.ShouldBe(2);
			await Should.ThrowAsync<BusinessException>(() => service.PrepareSelectionAsync(item, null));
			await service.PrepareSelectionAsync(item, ParentSelection(fixture, father));
			(await service.ResolveSelectedAsync(item))!.UserId.ShouldBe(father.UserId);
			await Should.ThrowAsync<BusinessException>(() => service.PrepareSelectionAsync(item, ParentSelection(fixture, mother)));
			var manager = GetRequiredService<IdentityUserManager>(); var user = await manager.GetByIdAsync(father.UserId);
			user.SetIsActive(false); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
			await Should.ThrowAsync<BusinessException>(() => service.ResolveSelectedAsync(item));
		});
	});

	[Theory]
	[InlineData("activation")]
	[InlineData("completion")]
	public Task Sibling_parent_onboarding_activation_reuses_parent_without_duplicate_delivery_under_automatic_school_settings(string timing) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(timing);
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var manager = GetRequiredService<IdentityUserManager>();
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp,
			IdempotencyKey = "sibling-parent-reuse", SiblingParentSelection = ParentSelection(fixture, parent) });
		activated.Status.ShouldBe(StudentOnboardingStatus.Completed); activated.CredentialsPending.ShouldBeFalse();
		activated.RequiredCredentialDeliveryConfirmed.ShouldBeNull();
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.IsLinked.ShouldBeTrue(); workspace.ParentSibling.Status.ShouldBe("Linked");
		workspace.Recipients.Single(x => x.Relationship == "father").CanSend.ShouldBeFalse();
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem().UserId.ShouldBe(parent.UserId);
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		(await WithUnitOfWorkAsync(() => requests.CountAsync(x => x.CaseId == activated.Id))).ShouldBe(0);
		var repeatedSend = await service.SendCredentialsAsync(activated.Id,
			new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father", ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		repeatedSend.ConcurrencyStamp.ShouldBe(workspace.ConcurrencyStamp);
		repeatedSend.Recipients.Single(x => x.Relationship == "father").CanSend.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentCredentialEmailAppService>().RequestAsync(activated.Id,
			new() { AccountType = "guardian", ConcurrencyStamp = workspace.ConcurrencyStamp }));
		await GetRequiredService<StudentCredentialEmailAppService>().QueueAsync(activated.Id, "guardian");
		(await WithUnitOfWorkAsync(() => requests.CountAsync(x => x.CaseId == activated.Id))).ShouldBe(0);
		await service.ActivateAsync(activated.Id, new() { IdempotencyKey = "sibling-parent-reuse", ConcurrencyStamp = activated.ConcurrencyStamp,
			SiblingParentSelection = ParentSelection(fixture, parent) });
		(await service.GetPortalAccountsAsync(activated.Id)).Count.ShouldBe(1);
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(passwordHash);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == activated.StudentId))).ShouldBe(1);
		using (_principalAccessor.Change(EligibilityPrincipal(parent.UserId, fixture.FatherEmail, "Parent")))
		{
			var owned = await service.GetMyPortalStudentsAsync();
			owned.Select(x => x.StudentId).OrderBy(x => x).ShouldBe(new[] { parent.StudentId, activated.StudentId }.OrderBy(x => x));
		}
	});

	[Fact]
	public Task Sibling_parent_onboarding_first_account_remains_manual_under_automatic_school_settings() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { IdempotencyKey = "first-parent-manual", ConcurrencyStamp = fixture.Case.ConcurrencyStamp });
		activated.StudentEnrollmentId.ShouldNotBeNull(); activated.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldBeEmpty();
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		(await WithUnitOfWorkAsync(() => requests.CountAsync(x => x.CaseId == activated.Id))).ShouldBe(0);
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.Recipients.Single(x => x.Relationship == "father").CanSend.ShouldBeTrue();
		workspace = await service.SendCredentialsAsync(activated.Id, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
			ExpectedEmail = fixture.FatherEmail, ConcurrencyStamp = workspace.ConcurrencyStamp });
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem();
		var request = await WithUnitOfWorkAsync(() => requests.GetAsync(x => x.CaseId == activated.Id && x.AccountType == "guardian"));
		request.RequiresSavedOwnerValidation().ShouldBeTrue(); request.Status.ShouldBe("Pending");
	});

	[Fact]
	public Task Sibling_parent_onboarding_does_not_grant_disabled_parent_access_even_when_a_confirmed_account_exists() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync(parentAccess: "not-needed");
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { IdempotencyKey = "disabled-parent", ConcurrencyStamp = fixture.Case.ConcurrencyStamp });
		activated.StudentEnrollmentId.ShouldNotBeNull();
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldBeEmpty();
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.Status.ShouldBe("NotRequired"); workspace.ParentSibling.Candidates.ShouldBeEmpty();
		await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(activated.Id,
			new() { ConcurrencyStamp = workspace.ConcurrencyStamp, SiblingParentSelection = ParentSelection(fixture, parent) }));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == activated.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Sibling_parent_onboarding_does_not_relabel_an_existing_mother_account_as_father() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var mother = await AddSiblingParentOnboardingSourceAsync(fixture, "mother");
		await ConfirmSiblingParentOnboardingAsync(fixture, mother);
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateFatherDetails(ParentDetails("Incorrect Father", fixture.MotherEmail)); family.UpdateMotherDetails(null);
			await families.UpdateAsync(family, true);
			var service = GetRequiredService<StudentSiblingParentService>(); var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			(await service.GetAsync(item)).Candidates.ShouldBeEmpty();
			await Should.ThrowAsync<BusinessException>(() => service.PrepareSelectionAsync(item, ParentSelection(fixture, mother)));
		});
	});

	[Fact]
	public Task Sibling_parent_onboarding_reuses_explicitly_confirmed_legacy_parent_without_relabeling_its_source_link() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", legacySource: true);
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { IdempotencyKey = "legacy-parent-reuse", ConcurrencyStamp = fixture.Case.ConcurrencyStamp });
		activated.Status.ShouldBe(StudentOnboardingStatus.Completed);
		var target = (await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem();
		target.UserId.ShouldBe(parent.UserId); target.Relationship.ShouldBe("father");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetAsync(parent.LinkId))).Relationship.ShouldBe("guardian");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == activated.Id))).ShouldBe(0);
	});

	[Theory]
	[InlineData("source")]
	[InlineData("email")]
	public Task Sibling_parent_onboarding_legacy_source_requires_the_exact_confirmed_child_and_email(string mismatch) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", legacySource: true);
		var other = await AddSiblingParentOnboardingSourceAsync(fixture, "mother");
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.ApplicantId);
			AdmissionSiblingParentSelection.Confirm(applicant, new AdmissionSiblingParentCandidateDto {
				SourceApplicantId = mismatch == "source" ? other.ApplicantId : parent.ApplicantId,
				Relationship = "father", Name = "Reviewed Father", Mobile = "9000000002",
				Email = mismatch == "email" ? "different-parent@example.test" : fixture.FatherEmail },
				TestUserId, DateTime.UtcNow, "9000000099", fixture.FatherEmail);
			await _applicantRepository.UpdateAsync(applicant, true);
			var sibling = GetRequiredService<StudentSiblingParentService>(); var item = await _onboardingCaseRepository.GetAsync(fixture.Case.Id);
			(await sibling.GetAsync(item)).Candidates.ShouldNotContain(x => x.Relationship == "father");
			await Should.ThrowAsync<BusinessException>(() => sibling.PrepareSelectionAsync(item, ParentSelection(fixture, parent)));
		});
	});

	[Theory]
	[InlineData("edit")]
	[InlineData("update")]
	public Task Sibling_parent_onboarding_direct_activation_preserves_exact_action_and_identity_permissions(string deniedAction) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var father = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, father);
		var permission = deniedAction == "edit" ? ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit : IdentityPermissions.Users.Update;
		var service = CreateDeniedCredentialsService(permission);
		var failure = await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => service.ActivateAsync(fixture.Case.Id,
			new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "denied-sibling-parent", SiblingParentSelection = ParentSelection(fixture, father) })));
		failure.Data["PolicyName"].ShouldBe(permission);
		(await GetRequiredService<IStudentOnboardingAppService>().GetAsync(fixture.Case.Id)).StudentEnrollmentId.ShouldBeNull();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(x => x.StudentId == fixture.Case.StudentId))).ShouldBe(0);
	});

	[Fact]
	public Task Sibling_parent_onboarding_failed_setup_retry_requires_access_to_the_original_case() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var father = await AddSiblingParentOnboardingSourceAsync(fixture, "father", confirmed: false);
		await ConfirmSiblingParentOnboardingAsync(fixture, father);
		var originalCaseId = await WithUnitOfWorkAsync(async () =>
		{
			var template = await _applicationRepository.GetAsync(fixture.Case.ApplicationId);
			var application = await _applicationRepository.InsertAsync(new(Guid.NewGuid(), TestTenantId, $"SOURCE-{Guid.NewGuid():N}",
				father.ApplicantId, null, template.AdmissionCycleId, fixture.Offering.Id, template.WorkflowDefinitionId, template.FormDefinitionId), true);
			var source = await _onboardingCaseRepository.InsertAsync(new(Guid.NewGuid(), TestTenantId, application.Id, father.ApplicantId,
				father.StudentId, fixture.Offering.Id, fixture.Case.AcademicYearId, fixture.Offering.BoardId, fixture.Offering.MediumId,
				fixture.Offering.StageId, fixture.Offering.StandardId, "source-parent-setup", DateTime.UtcNow, DateTime.UtcNow.AddDays(2)), true);
			return source.Id;
		});
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var original = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, originalCaseId, father.StudentId,
			father.LinkId, father.UserId, "guardian", fixture.FatherEmail, TestUserId, DateTime.UtcNow.AddMinutes(-4));
		original.Claim(DateTime.UtcNow.AddMinutes(-3)); original.Failed(DateTime.UtcNow.AddMinutes(-3));
		await WithUnitOfWorkAsync(() => requests.InsertAsync(original, true));
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "sibling-parent-failed-setup" });
		var staffId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(staffId, $"sibling-division-staff-{staffId:N}");
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
			userId: staffId, academicYearId: fixture.Case.AcademicYearId, educationBoardId: fixture.Offering.BoardId,
			educationMediumId: fixture.Offering.MediumId, educationMediumStageId: fixture.Offering.StageId,
			standardId: fixture.Offering.StandardId, divisionId: fixture.Case.DivisionId), true));
		using (_principalAccessor.Change(EligibilityPrincipal(staffId, "sibling-division-staff", "staff")))
		{
			var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
			workspace.ParentSibling!.Status.ShouldBe("SetupFailed"); workspace.ParentSibling.CanRetrySetup.ShouldBeFalse();
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(originalCaseId));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.RetryParentSetupAsync(fixture.Case.Id, new() { ConcurrencyStamp = workspace.ConcurrencyStamp }));
		}
		(await WithUnitOfWorkAsync(() => requests.GetAsync(original.Id))).Status.ShouldBe("Failed");
		(await WithUnitOfWorkAsync(() => requests.CountAsync(x => x.CaseId == fixture.Case.Id))).ShouldBe(0);
		(await service.GetPortalAccountsAsync(fixture.Case.Id)).ShouldHaveSingleItem().UserId.ShouldBe(father.UserId);
	});

	[Fact]
	public Task Sibling_parent_onboarding_retries_only_original_failed_setup_and_dispatches_without_password_or_email_duplication() => RunCertificationScenarioAsync(async () =>
	{
		var original = await CreateSiblingParentOnboardingFixtureAsync();
		var service = GetRequiredService<IStudentOnboardingAppService>();
		await service.ActivateAsync(original.Case.Id, new() { ConcurrencyStamp = original.Case.ConcurrencyStamp, IdempotencyKey = "original-parent-manual" });
		var firstWorkspace = await service.GetCredentialsAsync(original.Case.Id);
		await service.SendCredentialsAsync(original.Case.Id, new() { AccountType = "guardian", ConfirmRecipientAccess = true, Relationship = "father",
			ExpectedEmail = original.FatherEmail, ConcurrencyStamp = firstWorkspace.ConcurrencyStamp });
		var parent = (await service.GetPortalAccountsAsync(original.Case.Id)).ShouldHaveSingleItem();
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var firstRequest = await WithUnitOfWorkAsync(() => requests.GetAsync(x => x.CaseId == original.Case.Id && x.AccountType == "guardian"));
		var sender = Substitute.For<IEmailSender>();
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>())
			.Returns(Task.FromException(new InvalidOperationException("Controlled transport failure.")));
		var provider = GetRequiredService<IServiceProvider>();
		// The application-test module has no browser URL. Keep real eligibility logic,
		// supplying only the absolute test login origin needed to render setup links.
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example.test" }).Build();
		var resolver = ActivatorUtilities.CreateInstance<StudentOnboardingCredentialEligibilityResolver>(provider, configuration);
		var dispatcher = ActivatorUtilities.CreateInstance<StudentCredentialEmailDispatcher>(provider, sender, resolver);
		await dispatcher.DispatchAsync(firstRequest.Id);
		var failed = await WithUnitOfWorkAsync(() => requests.GetAsync(firstRequest.Id));
		failed.Status.ShouldBe("Failed"); failed.EncryptedInitialPassword.ShouldNotBeNull();
		var manager = GetRequiredService<IdentityUserManager>();
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldNotBeNull();
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await requests.GetAsync(firstRequest.Id);
			typeof(StudentCredentialEmailRequest).GetProperty(nameof(StudentCredentialEmailRequest.RequestedAt))!.SetValue(row, DateTime.UtcNow.AddMinutes(-4));
			await requests.UpdateAsync(row, true);
		});
		var applicant = await WithUnitOfWorkAsync(async () =>
		{
			var source = await _applicantRepository.GetAsync(original.ApplicantId);
			var sibling = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId, "Second", null, "Sibling",
				CertificationBirthDate, "Male", null, Guid.NewGuid(), "14 School Road", null, "Ahmedabad", "Gujarat", "India", "380001");
			AdmissionSiblingParentSelection.Confirm(sibling, new AdmissionSiblingParentCandidateDto { SourceApplicantId = source.Id,
				Relationship = "father", Name = "Sibling Father", Mobile = "9000000002", Email = original.FatherEmail },
				TestUserId, DateTime.UtcNow, "9000000099", original.FatherEmail);
			return await _applicantRepository.InsertAsync(sibling, true);
		});
		var sharedStructureId = await WithUnitOfWorkAsync(async () =>
			(await _applicationRepository.GetAsync(original.Case.ApplicationId)).GetFeeSelection()!.StructureId);
		var second = await CreateSiblingParentOnboardingFixtureAsync(existingApplicant: applicant, sharedFatherEmail: original.FatherEmail, sharedStructureId: sharedStructureId);
		await service.ActivateAsync(second.Case.Id, new() { ConcurrencyStamp = second.Case.ConcurrencyStamp, IdempotencyKey = "second-parent-reuse" });
		var secondWorkspace = await service.GetCredentialsAsync(second.Case.Id);
		secondWorkspace.ParentSibling!.Status.ShouldBe("SetupFailed"); secondWorkspace.ParentSibling.CanRetrySetup.ShouldBeTrue();
		await service.RetryParentSetupAsync(second.Case.Id, new() { ConcurrencyStamp = secondWorkspace.ConcurrencyStamp });
		(await WithUnitOfWorkAsync(() => requests.GetAsync(firstRequest.Id))).Status.ShouldBe("Pending");
		(await WithUnitOfWorkAsync(() => requests.CountAsync(x => x.CaseId == second.Case.Id))).ShouldBe(0);
		sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<AdditionalEmailSendingArgs?>()).Returns(Task.CompletedTask);
		await dispatcher.DispatchAsync(firstRequest.Id);
		await dispatcher.DispatchAsync(firstRequest.Id);
		var sent = await WithUnitOfWorkAsync(() => requests.GetAsync(firstRequest.Id));
		sent.Status.ShouldBe("Sent"); sent.AttemptCount.ShouldBe(2); sent.EncryptedInitialPassword.ShouldBeNull();
		sender.ReceivedCalls().Count(x => x.GetMethodInfo().Name == nameof(IEmailSender.SendAsync)).ShouldBe(2);
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(passwordHash);
		(await service.GetPortalAccountsAsync(second.Case.Id)).ShouldHaveSingleItem().UserId.ShouldBe(parent.UserId);
	});

	[Theory]
	[InlineData("required", StudentOnboardingStatus.NeedsAttention)]
	[InlineData("optional", StudentOnboardingStatus.Completed)]
	public Task Sibling_parent_onboarding_late_link_reuses_parent_after_required_or_optional_activation(string access, StudentOnboardingStatus beforeStatus) => RunCertificationScenarioAsync(async () =>
	{
		var late = await PrepareLateSiblingParentLinkAsync(access);
		late.Activated.Status.ShouldBe(beforeStatus);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var manager = GetRequiredService<IdentityUserManager>();
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(late.Parent.UserId))).PasswordHash;
		var workspace = await service.GetCredentialsAsync(late.Activated.Id);
		workspace.ParentSibling!.IsLinked.ShouldBeFalse(); workspace.ParentSibling.Status.ShouldBe("Available");
		workspace = await service.LinkParentAsync(late.Activated.Id, new() { ConcurrencyStamp = workspace.ConcurrencyStamp,
			SiblingParentSelection = ParentSelection(late.Child, late.Parent) });
		workspace.ParentSibling!.IsLinked.ShouldBeTrue(); workspace.ParentSibling.Status.ShouldBe("Linked");
		var completed = await service.GetAsync(late.Activated.Id);
		completed.Status.ShouldBe(StudentOnboardingStatus.Completed); completed.StudentEnrollmentId.ShouldBe(late.Activated.StudentEnrollmentId);
		await service.LinkParentAsync(completed.Id, new() { ConcurrencyStamp = workspace.ConcurrencyStamp,
			SiblingParentSelection = ParentSelection(late.Child, late.Parent) });
		(await service.GetPortalAccountsAsync(completed.Id)).ShouldHaveSingleItem().UserId.ShouldBe(late.Parent.UserId);
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(late.Parent.UserId))).PasswordHash.ShouldBe(passwordHash);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == completed.StudentId))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == completed.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Sibling_parent_onboarding_late_link_preserves_return_authorization_for_a_completed_optional_case() => RunCertificationScenarioAsync(async () =>
	{
		var late = await PrepareLateSiblingParentLinkAsync("optional");
		late.Activated.Status.ShouldBe(StudentOnboardingStatus.Completed);
		var item = await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _onboardingCaseRepository.GetAsync(late.Activated.Id);
			saved.ExtraProperties["RecordReturnTransitionId"] = Guid.NewGuid().ToString("D");
			return await _onboardingCaseRepository.UpdateAsync(saved, true);
		});
		var manager = GetRequiredService<IdentityUserManager>();
		var passwordHash = (await WithUnitOfWorkAsync(() => manager.GetByIdAsync(late.Parent.UserId))).PasswordHash;
		await Should.ThrowAsync<EntityNotFoundException>(() => GetRequiredService<IStudentOnboardingAppService>().LinkParentAsync(item.Id,
			new() { ConcurrencyStamp = item.ConcurrencyStamp, SiblingParentSelection = ParentSelection(late.Child, late.Parent) }));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(x => x.StudentId == item.StudentId))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == item.Id))).ShouldBe(0);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == item.StudentId))).ShouldBe(1);
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(late.Parent.UserId))).PasswordHash.ShouldBe(passwordHash);
	});

	[Fact]
	public Task Sibling_parent_onboarding_late_link_rejects_stale_tenant_scope_and_identity_drift() => RunCertificationScenarioAsync(async () =>
	{
		var late = await PrepareLateSiblingParentLinkAsync("required"); var service = GetRequiredService<IStudentOnboardingAppService>();
		var input = new LinkStudentSiblingParentDto { ConcurrencyStamp = "stale", SiblingParentSelection = ParentSelection(late.Child, late.Parent) };
		(await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(late.Activated.Id, input))).Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
		input.ConcurrencyStamp = (await service.GetCredentialsAsync(late.Activated.Id)).ConcurrencyStamp;
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.LinkParentAsync(late.Activated.Id, input));
		using (_principalAccessor.Change(EligibilityPrincipal(Guid.NewGuid(), "late-parent-no-scope", "staff")))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.LinkParentAsync(late.Activated.Id, input));
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var parent = await manager.GetByIdAsync(late.Parent.UserId);
			parent.SetIsActive(false); (await manager.UpdateAsync(parent)).Succeeded.ShouldBeTrue();
		});
		await Should.ThrowAsync<BusinessException>(() => service.LinkParentAsync(late.Activated.Id, input));
		(await service.GetPortalAccountsAsync(late.Activated.Id)).ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == late.Activated.Id))).ShouldBe(0);
	});

	[Theory]
	[InlineData("edit")]
	[InlineData("update")]
	public Task Sibling_parent_onboarding_direct_late_link_preserves_exact_action_and_identity_permissions(string deniedAction) => RunCertificationScenarioAsync(async () =>
	{
		var late = await PrepareLateSiblingParentLinkAsync("required");
		var permission = deniedAction == "edit" ? ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit : IdentityPermissions.Users.Update;
		var service = CreateDeniedCredentialsService(permission);
		var failure = await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => service.LinkParentAsync(late.Activated.Id,
			new() { ConcurrencyStamp = late.Activated.ConcurrencyStamp, SiblingParentSelection = ParentSelection(late.Child, late.Parent) })));
		failure.Data["PolicyName"].ShouldBe(permission);
		(await GetRequiredService<IStudentOnboardingAppService>().GetPortalAccountsAsync(late.Activated.Id)).ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(x => x.CaseId == late.Activated.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Sibling_parent_onboarding_dispatcher_cancels_a_preexisting_sibling_intent_before_password_or_transport() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father", withoutPassword: true);
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "queued-sibling-intent" });
		var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
		var link = await WithUnitOfWorkAsync(() => links.GetAsync(x => x.StudentId == activated.StudentId && x.AccountType == "guardian"));
		var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
		var queued = new StudentCredentialEmailRequest(Guid.NewGuid(), TestTenantId, activated.Id, activated.StudentId,
			link.Id, parent.UserId, "guardian", fixture.FatherEmail, TestUserId, DateTime.UtcNow.AddMinutes(-4));
		queued.RequireSavedOwnerValidation();
		await WithUnitOfWorkAsync(() => requests.InsertAsync(queued, true));
		var manager = GetRequiredService<IdentityUserManager>();
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBeNull();
		var sender = Substitute.For<IEmailSender>();
		// ActivatorUtilities resolves the real sibling service, including the optional
		// delivery guard, just as production DI does; only transport is substituted.
		var dispatcher = ActivatorUtilities.CreateInstance<StudentCredentialEmailDispatcher>(GetRequiredService<IServiceProvider>(), sender);
		await dispatcher.DispatchAsync(queued.Id);
		await dispatcher.DispatchAsync(queued.Id);
		var cancelled = await WithUnitOfWorkAsync(() => requests.GetAsync(queued.Id));
		cancelled.Status.ShouldBe("Cancelled"); cancelled.AttemptCount.ShouldBe(0); cancelled.EncryptedInitialPassword.ShouldBeNull();
		sender.ReceivedCalls().ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => manager.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBeNull();
	});

	private async Task<LateSiblingParentFixture> PrepareLateSiblingParentLinkAsync(string access)
	{
		var child = await CreateSiblingParentOnboardingFixtureAsync(parentAccess: access);
		var parent = await AddSiblingParentOnboardingSourceAsync(child, "father"); await ConfirmSiblingParentOnboardingAsync(child, parent);
		var manager = GetRequiredService<IdentityUserManager>();
		await WithUnitOfWorkAsync(async () =>
		{
			var user = await manager.GetByIdAsync(parent.UserId); user.SetIsActive(false); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
		});
		var activated = await GetRequiredService<IStudentOnboardingAppService>().ActivateAsync(child.Case.Id,
			new() { ConcurrencyStamp = child.Case.ConcurrencyStamp, IdempotencyKey = "late-parent-before-available" });
		await WithUnitOfWorkAsync(async () =>
		{
			var user = await manager.GetByIdAsync(parent.UserId); user.SetIsActive(true); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
		});
		return new(child, parent, activated);
	}

	private async Task<SiblingParentOnboardingFixture> CreateSiblingParentOnboardingFixtureAsync(string timing = "activation", string parentAccess = "required",
		AdmissionApplicant? existingApplicant = null, string? sharedFatherEmail = null, Guid? sharedStructureId = null)
	{
		var fixture = existingApplicant == null ? await CreateAutomaticRegressionFixtureAsync("SiblingParentOnboarding")
			: await CreateCertificationFixtureAsync("SecondSibling", existingApplicant: existingApplicant, currentWorkflow: true);
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
		if (existingApplicant != null)
		{
			await ConfigureOfferingReadinessAsync(fixture.Offering, cycle.AcademicYearId);
			await SaveAutomaticAccessAsync(cycle.AcademicYearId, student: false, guardian: false);
		}
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(x => x.AcademicYearId == cycle.AcademicYearId);
		var configuration = JsonNode.Parse(settings.ConfigurationJson)!.AsObject(); var access = configuration["readinessAccess"]!.AsObject();
		access["studentAccount"] = false; access["studentAccountAccess"] = "not-needed";
		access["guardianAccount"] = parentAccess == "required"; access["guardianAccountAccess"] = parentAccess;
		access["sendStudentCredentialEmail"] = false; access["sendGuardianCredentialEmail"] = true;
		access["credentialSendingTime"] = timing; access["credentialProvisioningMode"] = "automatic";
		await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId, ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
		var fatherEmail = sharedFatherEmail ?? $"sibling-father-{Guid.NewGuid():N}@example.test";
		var motherEmail = $"sibling-mother-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
			AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Sibling Father", "9000000002", fatherEmail);
			await _applicantRepository.UpdateAsync(applicant, true);
			var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("Parent")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", TestTenantId))).Succeeded.ShouldBeTrue();
		});
		var item = sharedStructureId.HasValue ? await PrepareSiblingParentWithSharedStructureAsync(fixture, sharedStructureId.Value)
			: await PrepareAutomaticRegressionCaseAsync(fixture, true, "sibling-parent-setup", confirmFeesBeforePlacement: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.GetAsync(item.StudentId);
			family.UpdateFatherDetails(ParentDetails("Sibling Father", fatherEmail)); family.UpdateMotherDetails(ParentDetails("Sibling Mother", motherEmail));
			await families.UpdateAsync(family, true);
		});
		return new(item, fixture.Offering, fixture.Applicant.Id, fatherEmail, motherEmail);
	}

	private async Task<StudentOnboardingCaseDto> PrepareSiblingParentWithSharedStructureAsync(AdmissionCertificationFixture fixture, Guid structureId)
	{
		var finalized = await FinalizeAsync(fixture.Application, true, "sibling-shared-structure");
		var service = GetRequiredService<IStudentOnboardingAppService>(); var item = await service.GetAsync(finalized.OnboardingCaseId);
		await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structureId, ConcurrencyStamp = item.ConcurrencyStamp });
		var division = await WithUnitOfWorkAsync(async () =>
			await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(
				await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, $"Sibling-{Guid.NewGuid():N}"[..24], 40), true));
		item = await service.GetAsync(item.Id);
		item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
		item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
		return await service.GetAsync(item.Id);
	}

	private async Task<SiblingParentSource> AddSiblingParentOnboardingSourceAsync(SiblingParentOnboardingFixture fixture, string relationship, bool confirmed = true, bool legacySource = false, bool withoutPassword = false)
	{
		var student = await CreateExistingStudentAsync(fixture.Offering, "ExistingParent", relationship);
		return await WithUnitOfWorkAsync(async () =>
		{
			var target = await _applicantRepository.GetAsync(fixture.ApplicantId); var sourceId = Guid.NewGuid();
			await _applicantRepository.InsertAsync(new(sourceId, TestTenantId, target.FamilyId, "Existing", null, "Sibling", DateTime.Today.AddYears(-8), "Male", student.Id, Guid.NewGuid()), true);
			var email = relationship == "father" ? fixture.FatherEmail : fixture.MotherEmail;
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>(); var family = await families.FindAsync(student.Id);
			if (family == null) family = await families.InsertAsync(new StudentFamilyDetail(student.Id, TestTenantId), true);
			if (legacySource) family.UpdateGuardianDetails(ParentDetails("Previously recorded parent", email));
			else if (relationship == "father") family.UpdateFatherDetails(ParentDetails("Sibling Father", email)); else family.UpdateMotherDetails(ParentDetails("Sibling Mother", email));
			await families.UpdateAsync(family, true);
			var manager = GetRequiredService<IdentityUserManager>(); var user = new IdentityUser(Guid.NewGuid(), email, email, TestTenantId);
			(await (withoutPassword ? manager.CreateAsync(user) : manager.CreateAsync(user, "Existing-Parent-Password-29!"))).Succeeded.ShouldBeTrue();
			(await manager.AddToRoleAsync(user, "Parent")).Succeeded.ShouldBeTrue();
			user.SetEmailConfirmed(confirmed); user.SetShouldChangePasswordOnNextLogin(!confirmed); (await manager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
			var link = await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, student.Id, user.Id, "guardian", legacySource ? "guardian" : relationship, TestUserId), true);
			return new SiblingParentSource(sourceId, student.Id, user.Id, link.Id, relationship);
		});
	}

	private Task ConfirmSiblingParentOnboardingAsync(SiblingParentOnboardingFixture fixture, SiblingParentSource parent) => WithUnitOfWorkAsync(async () =>
	{
		var applicant = await _applicantRepository.GetAsync(fixture.ApplicantId);
		AdmissionSiblingParentSelection.Confirm(applicant, new AdmissionSiblingParentCandidateDto { SourceApplicantId = parent.ApplicantId,
			Relationship = parent.Relationship, Name = "Sibling Parent", Email = parent.Relationship == "father" ? fixture.FatherEmail : fixture.MotherEmail,
			Mobile = "9000000002" }, TestUserId, DateTime.UtcNow, "9000000099", fixture.FatherEmail);
		await _applicantRepository.UpdateAsync(applicant, true);
	});

	private static ParentGuardianDetails ParentDetails(string name, string email) => new(name, null, null, email, "9000000002", null, null, null, null, null, null, null, null);
	private static StudentSiblingParentSelectionDto ParentSelection(SiblingParentOnboardingFixture fixture, SiblingParentSource parent) =>
		new() { Relationship = parent.Relationship, ExpectedEmail = parent.Relationship == "father" ? fixture.FatherEmail : fixture.MotherEmail, ExpectedUserId = parent.UserId };
	private sealed record SiblingParentOnboardingFixture(StudentOnboardingCaseDto Case, AdmissionOffering Offering, Guid ApplicantId, string FatherEmail, string MotherEmail);
	private sealed record SiblingParentSource(Guid ApplicantId, Guid StudentId, Guid UserId, Guid LinkId, string Relationship);
	private sealed record LateSiblingParentFixture(SiblingParentOnboardingFixture Child, SiblingParentSource Parent, StudentOnboardingCaseDto Activated);
}
