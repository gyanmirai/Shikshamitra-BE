using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Management, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Staff, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Management, AdmissionEntryMode.Both)]
	public Task Special_registration_starts_at_form_and_retries_without_duplicates(AdmissionTrack track, AdmissionEntryMode mode) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync(track, mode);
		var input = SpecialRegistrationInput(offering.Id);
		var first = await _applicationAppService.StartDirectRegistrationAsync(input);
		(await _applicationAppService.StartDirectRegistrationAsync(input)).Id.ShouldBe(first.Id);
		var runtime = await _applicationAppService.GetWorkflowAsync(first.Id);
		runtime.Stages.Single(item => item.Id == runtime.CurrentStageInstanceId).StageType.ShouldBe(AdmissionWorkflowStageType.Form);
		runtime.Stages.ShouldNotContain(item => item.StageType == AdmissionWorkflowStageType.Inquiry);
		await WithUnitOfWorkAsync(async () =>
		{
			(await _applicationRepository.GetAsync(first.Id)).InquiryId.ShouldBeNull();
			(await _applicantRepository.CountAsync(item => item.Id == input.RequestId)).ShouldBe(1);
			(await _applicationRepository.CountAsync(item => item.ApplicantId == input.RequestId)).ShouldBe(1);
		});
		input.ApplicantFirstName = "Different child";
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(input)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
	});

	[Theory]
	[InlineData(AdmissionTrack.General, AdmissionEntryMode.DirectRegistration)]
	[InlineData(AdmissionTrack.Management, AdmissionEntryMode.Inquiry)]
	[InlineData(AdmissionTrack.Staff, AdmissionEntryMode.Inquiry)]
	public Task Special_registration_rejects_general_or_inquiry_first_offerings(AdmissionTrack track, AdmissionEntryMode mode) => RunCertificationScenarioAsync(async () =>
	{
		var offering = mode == AdmissionEntryMode.Inquiry
			? await CreateOptionalIntakeOfferingAsync()
			: await CreateSpecialRegistrationOfferingAsync(track, mode);
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.UpdateRouting(track, mode, false);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var input = SpecialRegistrationInput(offering.Id);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(input)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed);
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, input.GuardianMobile)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationEntryNotAllowed);
		(await WithUnitOfWorkAsync(() => _applicantRepository.FindAsync(input.RequestId))).ShouldBeNull();
	});

	[Theory]
	[InlineData("inactive")]
	[InlineData("closed")]
	[InlineData("unconfigured")]
	public Task Special_registration_and_family_lookup_reject_unavailable_offerings(string reason) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			if (reason == "inactive") stored.Deactivate();
			if (reason == "closed") stored.UpdateOperationalDetails(stored.Code, false,
				DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-2), stored.Name);
			if (reason == "unconfigured") stored.ConfigureReferences(null, null, null);
			await _offeringRepository.UpdateAsync(stored, true);
		});
		var input = SpecialRegistrationInput(offering.Id);
		await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(input));
		await Should.ThrowAsync<BusinessException>(() => _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, input.GuardianMobile));
		(await WithUnitOfWorkAsync(() => _applicantRepository.FindAsync(input.RequestId))).ShouldBeNull();
	});

	[Fact]
	public Task Special_registration_finalizes_through_common_decision_and_onboarding() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var input = SpecialRegistrationInput(offering.Id);
		var application = await _applicationAppService.StartDirectRegistrationAsync(input);
		var frozen = await _applicationAppService.GetWorkflowAsync(application.Id);
		var reservedStudentId = (await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(application.ApplicantId))).ReservedStudentId;
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		runtime.WorkflowDefinitionId.ShouldBe(frozen.WorkflowDefinitionId);
		runtime.Stages.Single(stage => stage.Id == runtime.CurrentStageInstanceId).StageType.ShouldBe(AdmissionWorkflowStageType.Approval);
		var current = await _applicationAppService.GetAsync(application.Id);
		var approved = await _applicationAppService.ApproveAsync(application.Id, new()
		{
			ConcurrencyStamp = current.ConcurrencyStamp, Reason = "RTE documents and eligibility verified"
		});
		(await _applicationAppService.AdvanceWorkflowAsync(application.Id, new()
		{
			ExpectedCurrentStageInstanceId = runtime.CurrentStageInstanceId!.Value,
			ConcurrencyStamp = approved.ConcurrencyStamp, IdempotencyKey = "rte-common-decision"
		})).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
		var preview = await _applicationAppService.GetFinalizationPreviewAsync(application.Id);
		preview.IsReady.ShouldBeTrue(string.Join(" | ", preview.Items.Where(item => !item.Passed).Select(item => item.Detail)));
		preview.Items.ShouldNotContain(item => item.Key == "offer");
		var finalInput = new FinalizeAdmissionApplicationDto
		{
			ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
			IdempotencyKey = "rte-common-finalization", ConfirmCreateNew = true, ConfirmGuardianContactVerified = true
		};
		var finalized = await _applicationAppService.FinalizeAsync(application.Id, finalInput);
		(await _applicationAppService.FinalizeAsync(application.Id, finalInput)).StudentId.ShouldBe(finalized.StudentId);
		finalized.StudentId.ShouldBe(reservedStudentId);
		finalized.Application.Status.ShouldBe(AdmissionApplicationStatus.AdmissionFinalized);
		(await WithUnitOfWorkAsync(() => _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId)))
			.ApplicationId.ShouldBe(application.Id);
	});

	[Fact]
	public Task Special_registration_checks_scope_tenant_and_replay_access() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var input = SpecialRegistrationInput(offering.Id);
		var first = await _applicationAppService.StartDirectRegistrationAsync(input);
		var userId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(userId, $"special-scope-{userId:N}");
		using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity([
			new Claim(AbpClaimTypes.UserId, userId.ToString()),
			new Claim(AbpClaimTypes.UserName, $"special-scope-{userId:N}")], "Test"))))
		{
			await Should.ThrowAsync<EntityNotFoundException>(() => _applicationAppService.StartDirectRegistrationAsync(input));
			await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(SpecialRegistrationInput(offering.Id)));
			await Should.ThrowAsync<BusinessException>(() => _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, input.GuardianMobile));
		}
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(input));
			await Should.ThrowAsync<BusinessException>(() => _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, input.GuardianMobile));
		}
		(await _applicationAppService.GetAsync(first.Id)).ApplicantId.ShouldBe(input.RequestId);
	});

	[Fact]
	public Task Special_registration_requires_explicit_matching_family_choice() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var firstInput = SpecialRegistrationInput(offering.Id);
		var first = await _applicationAppService.StartDirectRegistrationAsync(firstInput);
		var matches = await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, firstInput.GuardianMobile);
		matches.Count.ShouldBe(1);
		var sibling = SpecialRegistrationInput(offering.Id);
		sibling.ApplicantFirstName = "Sibling";
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(sibling)))
			.Code.ShouldBe(AdmissionApplicantErrorCodes.DuplicateFamily);
		sibling.ExistingFamilyId = matches[0].FamilyId;
		sibling.GuardianMobile = "9876543211";
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(sibling)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationFamilyMismatch);
		sibling.GuardianMobile = firstInput.GuardianMobile;
		var second = await _applicationAppService.StartDirectRegistrationAsync(sibling);
		(await _applicationAppService.StartDirectRegistrationAsync(sibling)).Id.ShouldBe(second.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			(await _applicantRepository.GetAsync(first.ApplicantId)).FamilyId.ShouldBe(matches[0].FamilyId);
			(await _applicantRepository.GetAsync(second.ApplicantId)).FamilyId.ShouldBe(matches[0].FamilyId);
		});
		var inaccessibleFamily = await WithUnitOfWorkAsync(async () =>
		{
			var family = new AdmissionFamily(Guid.NewGuid(), TestTenantId, $"UNLINKED-{Guid.NewGuid():N}");
			var guardian = new AdmissionGuardian(Guid.NewGuid(), TestTenantId, family.Id,
				"Other", "Parent", "9123456789", null, AdmissionGuardianRelationship.Parent, true);
			family.SetPrimaryGuardian(guardian.Id);
			await _familyRepository.InsertAsync(family, true);
			await _guardianRepository.InsertAsync(guardian, true);
			return family;
		});
		(await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, "9123456789")).ShouldBeEmpty();
		var inaccessibleInput = SpecialRegistrationInput(offering.Id);
		inaccessibleInput.ExistingFamilyId = inaccessibleFamily.Id;
		inaccessibleInput.GuardianMobile = "9123456789";
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(inaccessibleInput)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationFamilyMismatch);
	});

	[Fact]
	public Task Offering_creation_allows_parallel_types_but_not_same_type_duplicates() => RunCertificationScenarioAsync(async () =>
	{
		var general = await CreateOptionalIntakeOfferingAsync();
		var direct = await CreateSpecialRegistrationOfferingAsync();
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var input = await EligibilityOfferingInputAsync<CreateAdmissionOfferingDto>(await service.GetAsync(general.Id));
		input.Track = AdmissionTrack.Rte;
		input.EntryMode = AdmissionEntryMode.DirectRegistration;
		input.Code = "RTE-SAME-CLASS";
		input.WorkflowDefinitionId = direct.WorkflowDefinitionId;
		input.IsActive = false;
		var created = await service.CreateAsync(general.AdmissionCycleId, input);
		created.Track.ShouldBe(AdmissionTrack.Rte);
		created.EntryMode.ShouldBe(AdmissionEntryMode.DirectRegistration);
		created.InquiryFormDefinitionId.ShouldBeNull();
		created.ApplicationFormDefinitionId.ShouldBe(direct.ApplicationFormDefinitionId);
		input.Code = "RTE-DUPLICATE-CODE";
		(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(general.AdmissionCycleId, input)))
			.Code.ShouldBe(AdmissionErrorCodes.OfferingDuplicate);
		var existing = await WithUnitOfWorkAsync(() => _offeringRepository.GetListAsync(item => item.AdmissionCycleId == general.AdmissionCycleId));
		existing.Count.ShouldBe(2);
		existing.ShouldContain(item => item.Track == AdmissionTrack.General);
		existing.ShouldContain(item => item.Track == AdmissionTrack.Rte);
	});

	[Fact]
	public Task Bulk_offering_preview_and_commit_keep_selected_type_and_type_aware_duplicate_check() => RunCertificationScenarioAsync(async () =>
	{
		var general = await CreateOptionalIntakeOfferingAsync();
		var direct = await CreateSpecialRegistrationOfferingAsync();
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var input = new BulkCreateAdmissionOfferingsDto
		{
			BoardId = general.BoardId, MediumId = general.MediumId, Track = AdmissionTrack.Rte,
			EntryMode = AdmissionEntryMode.DirectRegistration, WorkflowDefinitionId = direct.WorkflowDefinitionId,
			UseCycleDates = true, IsActive = false, PreviewOnly = true,
			Items = [new() { StandardId = general.StandardId, Code = "BULK-RTE" }]
		};
		(await service.BulkCreateAsync(general.AdmissionCycleId, input)).CanCommit.ShouldBeTrue();
		input.PreviewOnly = false;
		var created = await service.BulkCreateAsync(general.AdmissionCycleId, input);
		created.CreatedCount.ShouldBe(1);
		(await service.GetAsync(created.Items[0].OfferingId!.Value)).Track.ShouldBe(AdmissionTrack.Rte);
		input.PreviewOnly = true;
		var duplicate = await service.BulkCreateAsync(general.AdmissionCycleId, input);
		duplicate.CanCommit.ShouldBeFalse();
		duplicate.Items[0].Errors.ShouldContain(item => item.Code == AdmissionOfferingValidationCodes.Duplicate);
	});

	[Fact]
	public Task Used_offering_routing_remains_frozen_during_updates() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		await _applicationAppService.StartDirectRegistrationAsync(SpecialRegistrationInput(offering.Id));
		var service = GetRequiredService<IAdmissionOfferingAppService>();
		var current = await service.GetAsync(offering.Id);
		var input = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(current);
		input.Track = AdmissionTrack.General;
		input.EntryMode = AdmissionEntryMode.Inquiry;
		input.ConcurrencyStamp = current.ConcurrencyStamp;
		var saved = await service.UpdateAsync(offering.Id, input);
		saved.Track.ShouldBe(AdmissionTrack.Rte);
		saved.EntryMode.ShouldBe(AdmissionEntryMode.DirectRegistration);
	});

	[Fact]
	public Task Offering_write_rejects_invalid_routing_instead_of_normalizing_to_general() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var input = await EligibilityOfferingInputAsync<CreateAdmissionOfferingDto>(await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(offering.Id));
		input.Track = (AdmissionTrack)99;
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => _offeringValidator.ValidateAsync(offering.AdmissionCycleId, input)));
		input.Track = AdmissionTrack.General;
		input.EntryMode = (AdmissionEntryMode)99;
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => _offeringValidator.ValidateAsync(offering.AdmissionCycleId, input)));
	});

	private async Task<AdmissionOffering> CreateSpecialRegistrationOfferingAsync(
		AdmissionTrack track = AdmissionTrack.Rte, AdmissionEntryMode mode = AdmissionEntryMode.DirectRegistration)
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		return await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			stored.UpdateRouting(track, mode, false);
			stored.ConfigureReferences(offering.WorkflowDefinitionId, mode == AdmissionEntryMode.DirectRegistration ? null : stored.InquiryFormDefinitionId, stored.ApplicationFormDefinitionId);
			await _offeringRepository.UpdateAsync(stored, true);
			(await _offeringValidator.ValidateStoredAsync(stored)).Errors.ShouldBeEmpty();
			return stored;
		});
	}

	private static StartAdmissionDirectRegistrationDto SpecialRegistrationInput(Guid offeringId) => new()
	{
		RequestId = Guid.NewGuid(), AdmissionOfferingId = offeringId,
		ApplicantFirstName = "Asha", ApplicantLastName = "Patel", ApplicantDateOfBirth = new DateTime(2018, 5, 15), ApplicantGender = "Female",
		GuardianFirstName = "Ravi", GuardianLastName = "Patel", GuardianMobile = "9876543210", GuardianRelationship = AdmissionGuardianRelationship.Parent
	};
}
