using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("optional", true)]
	[InlineData("not-needed", false)]
	public Task Guardian_access_capability_distinguishes_optional_from_disabled_policy(string access, bool enabled) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation", emailEnabled: false,
			studentAccess: "not-needed", guardianAccess: access);
		var workspace = await GetRequiredService<IStudentOnboardingAppService>().GetCredentialsAsync(fixture.Case.Id);
		using var json = JsonDocument.Parse(JsonSerializer.Serialize(workspace, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault }));
		json.RootElement.TryGetProperty("guardianAccessEnabled", out var capability).ShouldBeTrue();
		capability.GetBoolean().ShouldBe(enabled);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Confirmed_guardian_intent_prevents_automatic_parent_attempt_in_a_legacy_case(bool confirmedGuardian) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateAutomaticRegressionFixtureAsync("LegacyGuardianChoice");
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(value => value.AcademicYearId == cycle.AcademicYearId);
		var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
		var access = config["readinessAccess"]!.AsObject();
		access["studentAccount"] = false; access["studentAccountAccess"] = "not-needed";
		access["guardianAccount"] = true; access["guardianAccountAccess"] = "required";
		access["sendStudentCredentialEmail"] = false; access["sendGuardianCredentialEmail"] = false;
		access["credentialProvisioningMode"] = "automatic"; access["guardianAccountRelationship"] = "father";
		await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
			ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
		var item = await PrepareAutomaticRegressionCaseAsync(fixture, true, "legacy-guardian-choice", confirmFeesBeforePlacement: true);
		var fatherEmail = $"legacy-father-{Guid.NewGuid():N}@example.test";
		var guardianEmail = $"legacy-aunt-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
			AdmissionSiblingParentSelection.IsParentFlow(applicant).ShouldBeFalse();
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(item.StudentId);
			family.UpdateFatherDetails(ParentDetails("Saved Father", fatherEmail));
			family.UpdateMotherDetails(new ParentGuardianDetails(null, null, null, null, null, null, null, null, null, null, null, null, null));
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", guardianEmail));
			await families.UpdateAsync(family, true);
			var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("Parent"))
				(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", TestTenantId))).Succeeded.ShouldBeTrue();
			(await _onboardingCaseRepository.GetAsync(item.Id)).GuardianContactVerifiedAt.ShouldNotBeNull();
		});
		var activated = await service.ActivateAsync(item.Id, new() { ConcurrencyStamp = item.ConcurrencyStamp,
			IdempotencyKey = "legacy-guardian-activation", GuardianAccessSelection = confirmedGuardian
				? new() { ExpectedEmail = guardianEmail, ConfirmRecipientAccess = true } : null });
		activated.StudentEnrollmentId.ShouldNotBeNull();
		activated.Events.ShouldNotContain(value => value.EventType == "AccessPending");
		var links = await service.GetPortalAccountsAsync(item.Id);
		if (confirmedGuardian)
		{
			links.ShouldBeEmpty();
			(await service.GetCredentialsAsync(item.Id)).ParentSibling.ShouldNotBeNull().GuardianAccessSelected.ShouldBeTrue();
		}
		else links.ShouldHaveSingleItem().Relationship.ShouldBe("father");
	});

	[Fact]
	public Task Optional_guardian_unlinked_intent_reconfirms_current_email_without_reusing_the_sibling_parent() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var email = $"reconfirm-aunt-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", email));
			await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var activated = await service.ActivateAsync(fixture.Case.Id, new() { ConcurrencyStamp = fixture.Case.ConcurrencyStamp,
			IdempotencyKey = "guardian-before-mail-correction", GuardianAccessSelection = new() { ExpectedEmail = email, ConfirmRecipientAccess = true } });
		var corrected = "corrected-" + email;
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", corrected));
			await families.UpdateAsync(family, true);
		});
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.GuardianAccessSelected.ShouldBeTrue();
		workspace.ParentSibling.Status.ShouldBe("ReviewRequired");
		workspace.ParentSibling.Candidates.ShouldBeEmpty();
		await service.ActivateAsync(activated.Id, new() { ConcurrencyStamp = workspace.ConcurrencyStamp,
			IdempotencyKey = "guardian-after-mail-correction", GuardianAccessSelection = new() { ExpectedEmail = corrected, ConfirmRecipientAccess = true } });
		workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.GuardianAccessSelected.ShouldBeTrue();
		workspace.ParentSibling.BlockedReason.ShouldBeNull();
		workspace.Recipients.Single(owner => owner.Relationship == "guardian").Email.ShouldBe(corrected);
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().GetAsync(parent.LinkId)))
			.UserId.ShouldBe(parent.UserId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>()
			.CountAsync(request => request.CaseId == activated.Id))).ShouldBe(0);
	});

	[Fact]
	public Task Optional_mother_link_projection_does_not_attribute_the_link_to_a_saved_father() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var mother = workspace.Recipients.Single(owner => owner.Relationship == "mother");
		await service.ProvisionPortalAccountAsync(workspace.CaseId, new() { AccountType = "guardian", Relationship = "mother",
			Email = mother.Email!, RelationshipVerified = true, ConcurrencyStamp = workspace.ConcurrencyStamp });
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		workspace.Recipients.Single(owner => owner.Relationship == "mother").HasLinkedAccount.ShouldBeTrue();
		workspace.Recipients.Single(owner => owner.Relationship == "mother").CanEditEmail.ShouldBeTrue();
		workspace.Recipients.Where(owner => owner.Relationship is "father" or "guardian").ShouldAllBe(owner => !owner.HasLinkedAccount && !owner.CanEditEmail);
	});

	[Fact]
	public Task Optional_guardian_link_projection_marks_only_the_exact_saved_guardian_and_keeps_email_repair_reviewed() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation");
		var email = $"linked-aunt-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", email));
			await families.UpdateAsync(family, true);
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var link = await service.ProvisionPortalAccountAsync(workspace.CaseId, new() { AccountType = "guardian", Relationship = "guardian",
			Email = email, RelationshipVerified = true, ConcurrencyStamp = workspace.ConcurrencyStamp });
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		var guardian = workspace.Recipients.Single(owner => owner.Relationship == "guardian");
		workspace.ParentSibling.ShouldNotBeNull().GuardianAccessSelected.ShouldBeTrue();
		guardian.HasLinkedAccount.ShouldBeTrue();
		guardian.CanEditEmail.ShouldBeFalse();
		guardian.EmailEditToken.ShouldBeEmpty();
		workspace.Recipients.Where(owner => owner.Relationship is "father" or "mother").ShouldAllBe(owner => !owner.HasLinkedAccount && !owner.CanEditEmail);
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", "changed-" + email));
			await families.UpdateAsync(family, true);
		});
		workspace = await service.GetCredentialsAsync(workspace.CaseId);
		workspace.Recipients.Where(owner => owner.AccountType == "guardian").ShouldAllBe(owner => !owner.HasLinkedAccount);
		workspace.ParentSibling.ShouldNotBeNull().GuardianAccessSelected.ShouldBeTrue();
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldHaveSingleItem().UserId.ShouldBe(link.UserId);
	});

	[Fact]
	public Task Optional_guardian_existing_email_collision_requires_review_instead_of_identity_reuse() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation");
		var email = $"existing-aunt-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", email));
			await families.UpdateAsync(family, true);
			var users = GetRequiredService<IdentityUserManager>();
			var user = new IdentityUser(Guid.NewGuid(), email, email, TestTenantId);
			(await users.CreateAsync(user, "Existing-Aunt-Password-29!")).Succeeded.ShouldBeTrue();
			(await users.AddToRoleAsync(user, "Parent")).Succeeded.ShouldBeTrue();
		});
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(owner => owner.Relationship == "guardian").CanSend.ShouldBeFalse();
		var command = JsonSerializer.Deserialize<SendStudentCredentialsDto>(JsonSerializer.Serialize(new {
			AccountType = "guardian", Relationship = "guardian", ExpectedEmail = email,
			ConcurrencyStamp = workspace.ConcurrencyStamp, ConfirmRecipientAccess = true
		}))!;
		await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId, command));
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
	});

	[Fact]
	public Task Optional_guardian_explicit_activation_preserves_sibling_parent_identity_and_never_auto_grants_guardian() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateSiblingParentOnboardingFixtureAsync();
		var parent = await AddSiblingParentOnboardingSourceAsync(fixture, "father");
		await ConfirmSiblingParentOnboardingAsync(fixture, parent);
		var guardianEmail = $"aunt-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", guardianEmail));
			await families.UpdateAsync(family, true);
		});
		var users = GetRequiredService<IdentityUserManager>();
		var parentPassword = (await WithUnitOfWorkAsync(() => users.GetByIdAsync(parent.UserId))).PasswordHash;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var command = JsonSerializer.Deserialize<ActivateStudentOnboardingDto>(JsonSerializer.Serialize(new {
			ConcurrencyStamp = fixture.Case.ConcurrencyStamp, IdempotencyKey = "guardian-instead-of-sibling-parent",
			GuardianAccessSelection = new { ExpectedEmail = guardianEmail, ConfirmRecipientAccess = true }
		}))!;
		var activated = await service.ActivateAsync(fixture.Case.Id, command);
		activated.StudentEnrollmentId.ShouldNotBeNull();
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldBeEmpty();
		var workspace = await service.GetCredentialsAsync(activated.Id);
		workspace.ParentSibling!.GuardianAccessSelected.ShouldBeTrue();
		workspace.Recipients.Single(owner => owner.Relationship == "father").CanSend.ShouldBeFalse();
		workspace.Recipients.Single(owner => owner.Relationship == "guardian").CanSend.ShouldBeTrue();
		var send = JsonSerializer.Deserialize<SendStudentCredentialsDto>(JsonSerializer.Serialize(new {
			AccountType = "guardian", Relationship = "guardian", ExpectedEmail = guardianEmail,
			ConcurrencyStamp = workspace.ConcurrencyStamp, ConfirmRecipientAccess = true
		}))!;
		await service.SendCredentialsAsync(activated.Id, send);
		var link = (await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem();
		link.Relationship.ShouldBe("guardian"); link.UserId.ShouldNotBe(parent.UserId); link.Email.ShouldBe(guardianEmail);
		(await WithUnitOfWorkAsync(() => users.GetByIdAsync(parent.UserId))).PasswordHash.ShouldBe(parentPassword);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>()
			.GetAsync(parent.LinkId))).Relationship.ShouldBe("father");
		var refreshed = await service.GetAsync(activated.Id);
		await service.ActivateAsync(activated.Id, new() { ConcurrencyStamp = refreshed.ConcurrencyStamp, IdempotencyKey = "same-guardian-no-relink" });
		(await service.GetPortalAccountsAsync(activated.Id)).ShouldHaveSingleItem().UserId.ShouldBe(link.UserId);
	});

	[Fact]
	public Task Optional_guardian_manual_send_requires_confirmation_and_rejects_existing_parent_replacement() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateOneClickCredentialsCaseAsync("activation");
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var guardianEmail = $"aunt-{Guid.NewGuid():N}@example.test";
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.GetAsync(fixture.Case.StudentId);
			family.UpdateGuardianDetails(ParentDetails("Saved Aunt", guardianEmail));
			await families.UpdateAsync(family, true);
		});
		var workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		var confirmationFailure = await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(() => service.SendCredentialsAsync(workspace.CaseId, new() {
			AccountType = "guardian", Relationship = "guardian", ExpectedEmail = guardianEmail, ConcurrencyStamp = workspace.ConcurrencyStamp }));
		confirmationFailure.ValidationErrors.ShouldContain(error => error.MemberNames.Any(member => member.EndsWith(nameof(SendStudentCredentialsDto.ConfirmRecipientAccess), StringComparison.OrdinalIgnoreCase)) &&
			error.ErrorMessage != null && error.ErrorMessage.Contains("Confirm this person may access this student", StringComparison.Ordinal));
		(await service.GetPortalAccountsAsync(workspace.CaseId)).ShouldBeEmpty();
		await LinkOneClickFixtureAccountsAsync(fixture);
		workspace = await service.GetCredentialsAsync(fixture.Case.Id);
		workspace.Recipients.Single(owner => owner.Relationship == "guardian").CanSend.ShouldBeFalse();
		var replacement = JsonSerializer.Deserialize<SendStudentCredentialsDto>(JsonSerializer.Serialize(new {
			AccountType = "guardian", Relationship = "guardian", ExpectedEmail = guardianEmail,
			ConcurrencyStamp = workspace.ConcurrencyStamp, ConfirmRecipientAccess = true
		}))!;
		await Should.ThrowAsync<BusinessException>(() => service.SendCredentialsAsync(workspace.CaseId, replacement));
		(await service.GetPortalAccountsAsync(workspace.CaseId)).Single(link => link.AccountType == "guardian").Relationship.ShouldBe("father");
	});
}
