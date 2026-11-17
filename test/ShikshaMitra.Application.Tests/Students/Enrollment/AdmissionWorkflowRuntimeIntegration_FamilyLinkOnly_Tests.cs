using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("Name", "FirstName", "Own Mother")]
	[InlineData("Mobile", "Mobile", "9876543210")]
	public void Family_only_optional_structured_clear_keeps_required_generic_contact(string suffix, string generic, string expected)
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), "Child", null, null, null, null, null, Guid.NewGuid());
		AdmissionPrimaryContactSelection.Capture(applicant, "mother", "Own Mother", "9876543210", "own@example.com", false, TestUserId, DateTime.UtcNow, false);
		AdmissionSiblingFamilyLink.CaptureContact(applicant, "Own Mother", null, "9876543210", "own@example.com");
		var key = "Mother." + suffix;
		AdmissionFormBindingRegistry.ApplyValue(key, string.Empty, applicant, null).ShouldBeTrue();
		string.IsNullOrEmpty(AdmissionStudentProfileMapping.Read(applicant, key)).ShouldBeTrue();
		AdmissionSiblingFamilyLink.SynchronizeStructuredContact(applicant, new System.Collections.Generic.Dictionary<string, string> { [key] = string.Empty });
		AdmissionFormBindingRegistry.GetValue("Guardian." + generic, applicant, null).ShouldBe(expected);
	}
	[Theory]
	[InlineData("Name", "Corrected Mother")]
	[InlineData("Mobile", "9876543211")]
	[InlineData("Email", "corrected@example.com")]
	public void Family_only_structured_active_parent_edits_update_own_generic_contact(string suffix, string value)
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), "Child", null, null, null, null, null, Guid.NewGuid());
		AdmissionPrimaryContactSelection.Capture(applicant, "mother", "Own Mother", "9876543210", "own@example.com", false, TestUserId, DateTime.UtcNow, false);
		AdmissionSiblingFamilyLink.CaptureContact(applicant, "Own Mother", null, "9876543210", "own@example.com");
		AdmissionFormBindingRegistry.ApplyValue("Mother." + suffix, value, applicant, null).ShouldBeTrue();
		var generic = suffix == "Name" ? "FirstName" : suffix;
		AdmissionFormBindingRegistry.GetValue("Guardian." + generic, applicant, null).ShouldBe(value);
		AdmissionFormBindingRegistry.ApplyValue("Father." + suffix, suffix == "Mobile" ? "9876543212" : suffix == "Email" ? "other@example.com" : "Other Father", applicant, null).ShouldBeTrue();
		AdmissionFormBindingRegistry.GetValue("Guardian." + generic, applicant, null).ShouldBe(value);
		if (suffix == "Email")
		{
			AdmissionFormBindingRegistry.ApplyValue("Guardian.Email", "generic@example.com", applicant, null);
			AdmissionSiblingFamilyLink.SynchronizeStructuredContact(applicant, new System.Collections.Generic.Dictionary<string, string>
				{ ["Mother.Email"] = value, ["Guardian.Email"] = "generic@example.com" });
			AdmissionFormBindingRegistry.GetValue("Guardian.Email", applicant, null).ShouldBe(value);
		}
	}

	[Fact]
	public Task Family_only_confirmation_accepts_a_visible_child_witness_without_parent_candidates() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await CreateExistingStudentAsync(offering, "Witness", "Child");
			student.UpdateFamilyInfo(null, null, null, null);
			await _studentRepository.UpdateAsync(student, true);
			var witness = await _applicantRepository.GetAsync(source.ApplicantId!.Value);
			witness.LinkStudent(student.Id); await _applicantRepository.UpdateAsync(witness, true);
			var matching = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId!.Value, "Matching", null, "Sibling", null, null, null, Guid.NewGuid());
			AdmissionSiblingParentSelection.CaptureParent(matching, "father", "Matching Father", "9876543210", "amit@example.com");
			await _applicantRepository.InsertAsync(matching, false);
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, "WITNESS-" + Guid.NewGuid().ToString("N")[..12], offering.AdmissionCycleId,
				offering.Id, "Matching sibling", "9876543210", "amit@example.com", AdmissionInquirySource.Manual, null);
			inquiry.LinkIdentity(source.FamilyId.Value, matching.Id);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
		});
		var matches = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionDirectFamilyMatchService>().FindAccessibleMatchesAsync("9876543210", "amit@example.com"));
		matches.Single(match => match.FamilyId == source.FamilyId).Parents.ShouldNotContain(parent => parent.SourceApplicantId == source.ApplicantId);
		var input = new ConfirmAdmissionInquiryFamilyDto { FamilyLinkOnly = true, ExistingFamilyId = source.FamilyId!.Value,
			ExistingParentApplicantId = source.ApplicantId!.Value, ConcurrencyStamp = target.ConcurrencyStamp };
		var result = await GetRequiredService<IAdmissionInquiryAppService>().ConfirmFamilyAsync(target.Id, input);
		result.FamilyId.ShouldBe(source.FamilyId); result.SiblingFamilyConfirmed.ShouldBeFalse(); LinkedSiblingFlag(result).ShouldBeTrue();
	});
	[Fact]
	public Task Family_only_confirmation_preserves_own_parent_and_registration_without_account_authority() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, target) = await CreatePendingFamilyConfirmationFixtureAsync(genericContact: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(target.ApplicantId!.Value);
			AdmissionPrimaryContactSelection.Capture(applicant, "mother", "Own Mother", "9876543210", "own@example.com", false, TestUserId, DateTime.UtcNow, false);
			applicant.SetProperty("AdmissionStudentField.Father.Name", "Own Father");
			await _applicantRepository.UpdateAsync(applicant, true);
			var family = await _familyRepository.GetAsync(target.FamilyId!.Value);
			var guardian = await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value);
			guardian.UpdateContact("Own Mother", null, "9876543210", "own@example.com");
			await _guardianRepository.UpdateAsync(guardian, true);
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var before = await service.GetAsync(target.Id);
		var sourceBefore = JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.ApplicantId!.Value)));
		var sourceFamily = await WithUnitOfWorkAsync(() => _familyRepository.GetAsync(source.FamilyId!.Value));
		var sourceGuardianBefore = JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _guardianRepository.GetAsync(sourceFamily.PrimaryGuardianId!.Value)));
		var input = JsonSerializer.Deserialize<ConfirmAdmissionInquiryFamilyDto>(JsonSerializer.Serialize(new
		{
			FamilyLinkOnly = true, ExistingFamilyId = source.FamilyId, ExistingParentApplicantId = source.ApplicantId, ConcurrencyStamp = before.ConcurrencyStamp
		}))!;
		var result = await service.ConfirmFamilyAsync(target.Id, input);
		result.FamilyId.ShouldBe(source.FamilyId);
		result.PrimaryContactRelationship.ShouldBe("mother");
		result.ParentRelationship.ShouldBe("mother");
		result.GuardianFirstName.ShouldBe("Own Mother");
		result.SiblingFamilyConfirmed.ShouldBeFalse();
		LinkedSiblingFlag(result).ShouldBeTrue();
		LinkedSiblingFlag(await service.GetAsync(source.Id)).ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _applicantRepository.GetAsync(target.ApplicantId!.Value);
			AdmissionSiblingParentSelection.ReadConfirmation(saved).ShouldBeNull();
			AdmissionStudentProfileMapping.Read(saved, "Father.Name").ShouldBe("Own Father");
			AdmissionStudentProfileMapping.Read(saved, "Mother.Name").ShouldBe("Own Mother");
			(await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(target.Id)).FormResponseSnapshotJson.ShouldBe(target.FormResponseSnapshotJson);
		});
		await service.ConfirmFamilyAsync(target.Id, input); // Same family remains idempotent with the original browser stamp.
		await MarkInheritedInquiryInterestedAsync(target.Id);
		var application = await _applicationAppService.StartAsync(target.ApplicantId!.Value, offering.Id, target.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		form.FamilyPrefill.ShouldBeNull();
		JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.ApplicantId!.Value))).ShouldBe(sourceBefore);
		JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _guardianRepository.GetAsync(sourceFamily.PrimaryGuardianId!.Value))).ShouldBe(sourceGuardianBefore);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionActivity, Guid>>().CountAsync(activity => activity.InquiryId == target.Id && activity.ActivityType == "SiblingFamilyConfirmed"))).ShouldBe(1);
	});

	private static bool LinkedSiblingFlag(AdmissionInquiryDto inquiry) =>
		(bool)(typeof(AdmissionInquiryDto).GetProperty("HasLinkedSiblings")?.GetValue(inquiry) ?? false);

	[Fact]
	public Task Family_only_confirmation_revalidates_scope_permissions_stamp_and_family_witness() => RunCertificationScenarioAsync(async () =>
	{
		var (_, source, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var permissions = Substitute.For<IPermissionChecker>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => ConfirmPendingFamilyWithPermissionsAsync(target, source, permissions, familyLinkOnly: true));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage).Returns(true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => ConfirmPendingFamilyWithPermissionsAsync(target, source, permissions, familyLinkOnly: true));
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		ConfirmAdmissionInquiryFamilyDto Request(Guid familyId, Guid sourceId, string stamp) =>
			JsonSerializer.Deserialize<ConfirmAdmissionInquiryFamilyDto>(JsonSerializer.Serialize(new
			{ FamilyLinkOnly = true, ExistingFamilyId = familyId, ExistingParentApplicantId = sourceId, ConcurrencyStamp = stamp }))!;
		(await Should.ThrowAsync<BusinessException>(() => service.ConfirmFamilyAsync(target.Id,
			Request(source.FamilyId!.Value, source.ApplicantId!.Value, "stale")))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.ConcurrencyConflict);
		await Should.ThrowAsync<BusinessException>(() => service.ConfirmFamilyAsync(target.Id,
			Request(target.FamilyId!.Value, source.ApplicantId!.Value, target.ConcurrencyStamp)));
		using (_currentTenant.Change(Guid.NewGuid()))
			await Should.ThrowAsync<Exception>(() => service.ConfirmFamilyAsync(target.Id,
				Request(source.FamilyId!.Value, source.ApplicantId!.Value, target.ConcurrencyStamp)));
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.ApplicantId!.Value))).FamilyId.ShouldBe(target.FamilyId!.Value);
		await MarkInheritedInquiryInterestedAsync(target.Id);
		await _applicationAppService.StartAsync(target.ApplicantId!.Value, target.AdmissionOfferingId!.Value, target.Id);
		var advanced = await service.GetAsync(target.Id);
		(await Should.ThrowAsync<BusinessException>(() => service.ConfirmFamilyAsync(target.Id,
			Request(source.FamilyId!.Value, source.ApplicantId!.Value, advanced.ConcurrencyStamp)))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidState);
	});

	[Fact]
	public Task Family_only_confirmation_rejects_a_source_outside_the_current_actors_scope() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, _, target) = await CreatePendingFamilyConfirmationFixtureAsync();
		var (_, foreignSource, _) = await CreatePendingFamilyConfirmationFixtureAsync(separateStandard: true);
		var actor = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(actor, "family-only-scoped-" + actor.ToString("N"));
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(offering.AdmissionCycleId));
		await WithUnitOfWorkAsync(() => _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: actor,
			academicYearId: cycle.AcademicYearId, educationBoardId: offering.BoardId, educationMediumId: offering.MediumId,
			educationMediumStageId: offering.StageId, standardId: offering.StandardId), true));
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(true);
		using (_principalAccessor.Change(ProfilePrincipal(actor)))
			await Should.ThrowAsync<Exception>(() => ConfirmPendingFamilyWithPermissionsAsync(target, foreignSource, permissions, familyLinkOnly: true));
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.ApplicantId!.Value))).FamilyId.ShouldBe(target.FamilyId!.Value);
	});

	[Fact]
	public Task Family_only_creation_and_subsequent_edits_keep_authored_contacts_on_this_child() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, _) = await CreatePendingFamilyConfirmationFixtureAsync(genericContact: true);
		var sourceFamily = await WithUnitOfWorkAsync(() => _familyRepository.GetAsync(source.FamilyId!.Value));
		var guardianBefore = JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _guardianRepository.GetAsync(sourceFamily.PrimaryGuardianId!.Value)));
		var answers = ConfiguredInquiryAnswers("Own parent", email: "own@example.com");
		answers.Single(answer => answer.FieldCode == "CHILD").ValueJson = "\"Family-only new child\"";
		var input = JsonSerializer.Deserialize<CreateAdmissionInquiryDto>(JsonSerializer.Serialize(new
		{
			FamilyLinkOnly = true, ExistingFamilyId = source.FamilyId, ExistingParentApplicantId = source.ApplicantId,
			ParentRelationship = "father", AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			offering.BoardId, offering.MediumId, offering.StandardId, FormValues = answers
		}))!;
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var created = await service.CreateAsync(input);
		created = await service.GetAsync(created.Id);
		created.GuardianFirstName.ShouldBe("Own parent"); created.Email.ShouldBe("own@example.com");
		created.SiblingFamilyConfirmed.ShouldBeFalse(); LinkedSiblingFlag(created).ShouldBeTrue();
		JsonSerializer.Deserialize<string>(created.FormValues.Single(answer => answer.FieldCode == "ADULT").ValueJson!).ShouldBe("Own parent");
		var edited = await service.UpdateAsync(created.Id, new UpdateAdmissionInquiryDto
		{
			ApplicantName = created.ApplicantName, Phone = created.NormalizedPhone, Email = created.Email, Source = created.Source,
			PreferredLanguageCode = created.PreferredLanguageCode, ConcurrencyStamp = created.ConcurrencyStamp,
			FormValues = answers.Select(answer => new AdmissionFormValueDto { FieldCode = answer.FieldCode,
				ValueJson = answer.FieldCode == "ADULT" ? "\"Own corrected parent\"" : answer.ValueJson }).ToList()
		});
		edited.GuardianFirstName.ShouldBe("Own corrected parent");
		await AddFirstParentContactFieldAsync(offering.ApplicationFormDefinitionId!.Value, "OWN_GENERIC_EMAIL", "Guardian.Email");
		await AddFirstParentContactFieldAsync(offering.ApplicationFormDefinitionId!.Value, "OWN_STRUCTURED_EMAIL", "Father.Email");
		await MarkInheritedInquiryInterestedAsync(created.Id);
		var application = await _applicationAppService.StartAsync(created.ApplicantId!.Value, offering.Id, created.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		form.FamilyPrefill.ShouldBeNull();
		var savedForm = await _applicationAppService.UpdateFormValuesAsync(application.Id, new UpdateAdmissionApplicationFormValuesDto
		{
			ConcurrencyStamp = form.ConcurrencyStamp,
			Values = [new() { FieldCode = "OWN_GENERIC_EMAIL", ValueJson = "\"registration@example.com\"" }]
		});
		JsonSerializer.Deserialize<string>(savedForm.Values.Single(value => value.FieldCode == "OWN_GENERIC_EMAIL").ValueJson!).ShouldBe("registration@example.com");
		(await service.GetAsync(created.Id)).Email.ShouldBe("registration@example.com");
		await _applicationAppService.UpdateFormValuesAsync(application.Id, new UpdateAdmissionApplicationFormValuesDto
		{
			ConcurrencyStamp = savedForm.ConcurrencyStamp,
			Values = [new() { FieldCode = "OWN_GENERIC_EMAIL", ValueJson = "\"generic@example.com\"" },
				new() { FieldCode = "OWN_STRUCTURED_EMAIL", ValueJson = "\"structured@example.com\"" }]
		});
		(await service.GetAsync(created.Id)).Email.ShouldBe("structured@example.com");
		JsonSerializer.Serialize(await WithUnitOfWorkAsync(() => _guardianRepository.GetAsync(sourceFamily.PrimaryGuardianId!.Value))).ShouldBe(guardianBefore);
	});

	[Fact]
	public Task Family_only_creation_does_not_create_a_shared_guardian_when_source_primary_is_missing() => RunCertificationScenarioAsync(async () =>
	{
		var (offering, source, _) = await CreatePendingFamilyConfirmationFixtureAsync();
		var family = await WithUnitOfWorkAsync(() => _familyRepository.GetAsync(source.FamilyId!.Value));
		await WithUnitOfWorkAsync(() => _guardianRepository.DeleteAsync(family.PrimaryGuardianId!.Value, true));
		var countBefore = await WithUnitOfWorkAsync(() => _guardianRepository.GetCountAsync());
		var input = JsonSerializer.Deserialize<CreateAdmissionInquiryDto>(JsonSerializer.Serialize(new
		{
			FamilyLinkOnly = true, ExistingFamilyId = source.FamilyId, ExistingParentApplicantId = source.ApplicantId,
			ParentRelationship = "father", AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			offering.BoardId, offering.MediumId, offering.StandardId, FormValues = ConfiguredInquiryAnswers("Own parent")
		}))!;
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var created = await service.GetAsync((await service.CreateAsync(input)).Id);
		created.GuardianFirstName.ShouldBe("Own parent");
		(await WithUnitOfWorkAsync(() => _guardianRepository.GetCountAsync())).ShouldBe(countBefore);
		(await WithUnitOfWorkAsync(() => _familyRepository.GetAsync(family.Id))).PrimaryGuardianId.ShouldBe(family.PrimaryGuardianId);
	});

	[Fact]
	public Task Family_only_finalization_retains_own_mother_and_requires_separate_parent_account_confirmation() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var sourceInput = ManagedInquiryInput(offering); sourceInput.ParentRelationship = "father";
		sourceInput.GuardianFirstName = "Source Father"; sourceInput.GuardianEmail = "source@example.com";
		var source = await service.CreateAsync(sourceInput);
		var input = ManagedInquiryInput(offering); input.ParentRelationship = "mother";
		input.GuardianFirstName = "Own Mother"; input.GuardianPhone = sourceInput.GuardianPhone; input.GuardianEmail = "own@example.com";
		input.ApplicantLastName = "FamilyOnly"; input.ExistingFamilyId = source.FamilyId; input.ExistingParentApplicantId = source.ApplicantId;
		typeof(CreateAdmissionInquiryDto).GetProperty("FamilyLinkOnly")!.SetValue(input, true);
		var inquiry = await service.CreateAsync(input);
		inquiry = await service.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		application = await _applicationAppService.GetAsync(application.Id);
		application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Own parent contact verified"));
		await CompleteCertificationWorkflowAsync(application.Id);
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(application.Id), true, "family-only-own-mother");
		await WithUnitOfWorkAsync(async () =>
		{
			var student = (await _studentRepository.GetWithDetailsAsync(finalized.StudentId)).ShouldNotBeNull();
			student.FamilyDetail!.MotherDetails!.FullName.ShouldBe("Own Mother");
			student.FamilyDetail.MotherDetails.Email.ShouldBe("own@example.com");
			student.FamilyDetail.GuardianDetails.ShouldBeNull();
			AdmissionSiblingParentSelection.ReadConfirmation(await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value)).ShouldBeNull();
			(await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().CountAsync(link => link.StudentId == finalized.StudentId)).ShouldBe(0);
		});
	});
}
