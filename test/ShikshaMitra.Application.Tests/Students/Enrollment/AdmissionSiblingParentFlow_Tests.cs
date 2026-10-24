using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Sibling_parent_direct_email_match_requires_confirmation_preserves_shared_parent_and_replay() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var firstInput = SpecialRegistrationInput(offering.Id);
		firstInput.ParentRelationship = "father";
		firstInput.GuardianEmail = "parent@example.com";
		var first = await _applicationAppService.StartDirectRegistrationAsync(firstInput);
		var matches = await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, "9876543211", " PARENT@EXAMPLE.COM ");
		var match = matches.ShouldHaveSingleItem();
		match.MatchByEmail.ShouldBeTrue();
		match.MatchByMobile.ShouldBeFalse();
		var parent = match.Parents.Single(candidate => candidate.Relationship == "father");
		parent.SourceApplicantId.ShouldBe(first.ApplicantId);
		var sibling = SpecialRegistrationInput(offering.Id);
		sibling.ParentRelationship = "father";
		sibling.GuardianEmail = "parent@example.com";
		sibling.GuardianMobile = "9876543211";
		sibling.ApplicantFirstName = "Anaya";
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(sibling)))
			.Code.ShouldBe(AdmissionApplicantErrorCodes.DuplicateFamily);
		sibling.ExistingFamilyId = match.FamilyId;
		sibling.ExistingParentApplicantId = parent.SourceApplicantId;
		var second = await _applicationAppService.StartDirectRegistrationAsync(sibling);
		(await _applicationAppService.StartDirectRegistrationAsync(sibling)).Id.ShouldBe(second.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId);
			var confirmation = AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldNotBeNull();
			confirmation.FamilyId.ShouldBe(match.FamilyId);
			confirmation.SourceApplicantId.ShouldBe(first.ApplicantId);
			confirmation.Relationship.ShouldBe("father");
			confirmation.Email.ShouldBe("parent@example.com");
			confirmation.ConfirmedBy.ShouldBe(TestUserId);
			confirmation.SubmittedMobile.ShouldBe("9876543211");
			AdmissionStudentProfileMapping.Read(applicant, "Father.Mobile").ShouldBe(firstInput.GuardianMobile);
			var family = await _familyRepository.GetAsync(match.FamilyId);
			(await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value)).Mobile.ShouldBe(firstInput.GuardianMobile);
		});
		sibling.GuardianEmail = "changed@example.com";
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(sibling)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationIdempotencyConflict);
	});

	[Fact]
	public Task Sibling_parent_matching_returns_both_known_parents_and_rejects_wrong_source() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var firstInput = SpecialRegistrationInput(offering.Id);
		firstInput.ParentRelationship = "father";
		firstInput.GuardianEmail = "father@example.com";
		var first = await _applicationAppService.StartDirectRegistrationAsync(firstInput);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(first.ApplicantId);
			applicant.SetProperty("AdmissionStudentField.Mother.Name", "Meera Patel");
			applicant.SetProperty("AdmissionStudentField.Mother.Email", "mother@example.com");
			applicant.SetProperty("AdmissionStudentField.Mother.Mobile", "9876543212");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var match = (await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, "", "mother@example.com")).ShouldHaveSingleItem();
		match.Parents.ShouldContain(parent => parent.Relationship == "father");
		match.Parents.ShouldContain(parent => parent.Relationship == "mother");
		var sibling = SpecialRegistrationInput(offering.Id);
		sibling.ApplicantFirstName = "Anaya"; sibling.ParentRelationship = "mother";
		sibling.GuardianEmail = "mother@example.com"; sibling.GuardianMobile = "9876543213";
		sibling.ExistingFamilyId = match.FamilyId; sibling.ExistingParentApplicantId = Guid.NewGuid();
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(sibling)))
			.Code.ShouldBe(AdmissionErrorCodes.DirectRegistrationFamilyMismatch);
		sibling.ExistingParentApplicantId = first.ApplicantId;
		var second = await _applicationAppService.StartDirectRegistrationAsync(sibling);
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(second.ApplicantId)))
			.GetProperty<string>("AdmissionStudentField.Mother.Email").ShouldBe("mother@example.com");
	});

	[Fact]
	public Task Sibling_parent_matching_uses_current_enrolled_father_and_mother_profiles() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var firstInput = SpecialRegistrationInput(offering.Id); firstInput.ParentRelationship = "father"; firstInput.GuardianEmail = "oldfather@example.com";
		var first = await _applicationAppService.StartDirectRegistrationAsync(firstInput);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(first.ApplicantId);
			var cycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
			var student = Student.CreateDirectRegistration(applicant.ReservedStudentId, TestTenantId, "Asha", null, "Patel", cycle.AcademicYearId,
				offering.BoardId, offering.MediumId, offering.StageId, null, null, new DateTime(2018, 5, 15), GenderEnum.Female, "9876543210");
			student.UpdateFamilyInfo(new ParentGuardianDetails("Ravi Patel", null, null, "currentfather@example.com", "9876543210", null, null, null, null, null, null, null, null),
				new ParentGuardianDetails("Meera Patel", null, null, "currentmother@example.com", "9876543212", null, null, null, null, null, null, null, null), null, null);
			await _studentRepository.InsertAsync(student, true);
			applicant.LinkStudent(student.Id); await _applicantRepository.UpdateAsync(applicant, true);
		});
		var match = (await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, "", "currentmother@example.com")).ShouldHaveSingleItem();
		match.Parents.ShouldContain(parent => parent.Relationship == "mother" && parent.Email == "currentmother@example.com");
		match.Parents.ShouldContain(parent => parent.Relationship == "father" && parent.Email == "currentfather@example.com");
		match.Parents.ShouldNotContain(parent => parent.Relationship == "father" && parent.Email == "oldfather@example.com");
	});

	[Fact]
	public Task Sibling_parent_inquiry_matches_email_and_authored_contacts_without_mobile_match() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var firstInput = ManagedInquiryInput(offering);
		firstInput.ParentRelationship = "mother"; firstInput.GuardianEmail = "mother@example.com";
		var first = await service.CreateAsync(firstInput);
		var matches = await service.DuplicateCheckAsync(new()
		{
			AdmissionCycleId = offering.AdmissionCycleId, GuardianPhone = "9876543213",
			ParentContacts = [new() { Relationship = "mother", Email = "MOTHER@EXAMPLE.COM" }]
		});
		var match = matches.Single(item => item.ApplicantId == first.ApplicantId);
		match.CanLinkFamily.ShouldBeTrue(); match.MatchByEmail.ShouldBeTrue(); match.MatchByMobile.ShouldBeFalse();
		var sibling = ManagedInquiryInput(offering);
		sibling.ParentRelationship = "mother"; sibling.GuardianEmail = "mother@example.com";
		sibling.GuardianPhone = "9876543213"; sibling.ExistingFamilyId = match.FamilyId;
		sibling.ExistingParentApplicantId = first.ApplicantId;
		sibling.ApplicantFirstName = "Anaya";
		sibling.FormValues.Single(value => value.FieldCode == "APPLICANT_FIRST_NAME").ValueJson = "\"Anaya\"";
		var second = await service.CreateAsync(sibling);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId!.Value);
			AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldNotBeNull().Relationship.ShouldBe("mother");
			AdmissionStudentProfileMapping.Read(applicant, "Mother.Email").ShouldBe("mother@example.com");
			var family = await _familyRepository.GetAsync(match.FamilyId);
			(await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value)).Mobile.ShouldBe(firstInput.GuardianPhone);
		});
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAppService>()
			.CreateFromPublicLinkAsync(sibling, null, offering.InquiryFormDefinitionId))))
			.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	});

	[Theory]
	[InlineData("guardian")]
	[InlineData("relative")]
	[InlineData("other")]
	public Task Sibling_parent_direct_rejects_non_parent_roles(string relationship) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var input = SpecialRegistrationInput(offering.Id); input.ParentRelationship = relationship;
		var validation = await new StartAdmissionDirectRegistrationDtoValidator().ValidateAsync(input);
		validation.IsValid.ShouldBeFalse();
	});

	[Fact]
	public Task Sibling_parent_helper_never_treats_legacy_generic_parent_as_trusted_confirmation()
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), "Aarav", null, "Patel", null, null, null, Guid.NewGuid());
		AdmissionSiblingParentSelection.IsParentFlow(applicant).ShouldBeFalse();
		AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldBeNull();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Ravi Patel", "9876543210", " PARENT@EXAMPLE.COM ");
		AdmissionSiblingParentSelection.IsParentFlow(applicant).ShouldBeTrue();
		AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldBeNull();
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe("parent@example.com");
		return Task.CompletedTask;
	}

	[Fact]
	public Task Sibling_parent_matching_does_not_add_a_new_name_and_birth_date_identity_block_to_direct_registration() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var firstInput = SpecialRegistrationInput(offering.Id);
		firstInput.ParentRelationship = "father"; firstInput.GuardianEmail = "parent@example.com";
		var first = await _applicationAppService.StartDirectRegistrationAsync(firstInput);
		var family = (await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, firstInput.GuardianMobile, firstInput.GuardianEmail)).ShouldHaveSingleItem();
		var sibling = SpecialRegistrationInput(offering.Id);
		sibling.ApplicantMiddleName = "Different";
		sibling.ParentRelationship = "father"; sibling.GuardianEmail = firstInput.GuardianEmail;
		sibling.ExistingFamilyId = family.FamilyId; sibling.ExistingParentApplicantId = first.ApplicantId;
		var second = await _applicationAppService.StartDirectRegistrationAsync(sibling);
		second.ApplicantId.ShouldNotBe(first.ApplicantId);
		(await _applicationAppService.StartDirectRegistrationAsync(sibling)).Id.ShouldBe(second.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(second.ApplicantId);
			applicant.FirstName.ShouldBe(firstInput.ApplicantFirstName);
			applicant.LastName.ShouldBe(firstInput.ApplicantLastName);
			applicant.DateOfBirth.ShouldBe(firstInput.ApplicantDateOfBirth);
			applicant.MiddleName.ShouldBe("Different");
		});
	});

	[Fact]
	public Task Sibling_parent_matching_does_not_add_a_new_name_and_birth_date_identity_block_to_inquiry() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var firstInput = ManagedInquiryInput(offering);
		firstInput.ParentRelationship = "mother"; firstInput.GuardianEmail = "parent@example.com";
		var first = await service.CreateAsync(firstInput);
		var sibling = ManagedInquiryInput(offering);
		sibling.ParentRelationship = "mother"; sibling.GuardianEmail = firstInput.GuardianEmail;
		sibling.ExistingFamilyId = first.FamilyId; sibling.ExistingParentApplicantId = first.ApplicantId;
		var second = await service.CreateAsync(sibling);
		second.ApplicantId.ShouldNotBe(first.ApplicantId);
		second.FamilyId.ShouldBe(first.FamilyId);
		second.ApplicantName.ShouldBe(first.ApplicantName);
	});

	[Theory]
	[InlineData(AdmissionGuardianRelationship.Guardian)]
	[InlineData(AdmissionGuardianRelationship.Relative)]
	public Task Sibling_parent_legacy_family_header_keeps_primary_name_without_offering_non_parent_candidates(AdmissionGuardianRelationship relationship) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var firstInput = SpecialRegistrationInput(offering.Id); firstInput.GuardianRelationship = relationship;
		await _applicationAppService.StartDirectRegistrationAsync(firstInput);
		var match = (await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, firstInput.GuardianMobile)).ShouldHaveSingleItem();
		match.PrimaryGuardianName.ShouldBe("Ravi Patel");
		match.Parents.ShouldBeEmpty();
		match.MatchByMobile.ShouldBeTrue();
		var secondInput = SpecialRegistrationInput(offering.Id); secondInput.GuardianRelationship = relationship;
		secondInput.ExistingFamilyId = match.FamilyId; secondInput.ApplicantFirstName = "Legacy sibling";
		var second = await _applicationAppService.StartDirectRegistrationAsync(secondInput);
		(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(second.ApplicantId))).FamilyId.ShouldBe(match.FamilyId);
	});

	[Fact]
	public Task Sibling_parent_confirmed_mother_prefills_generic_parent_aliases_without_changing_father_or_source()
	{
		var familyId = Guid.NewGuid();
		var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, familyId, "Anaya", null, "Patel", null, null, null, Guid.NewGuid());
		var father = new AdmissionGuardian(Guid.NewGuid(), TestTenantId, familyId, "Ravi", "Patel", "9876543210", "father@example.com", AdmissionGuardianRelationship.Parent, true);
		applicant.SetProperty("AdmissionStudentField.Father.Email", "father@example.com");
		AdmissionSiblingParentSelection.Confirm(applicant, new()
		{
			SourceApplicantId = Guid.NewGuid(), Relationship = "mother", Name = "Meera Patel", FirstName = "Meera", LastName = "Patel",
			Email = "mother@example.com", Mobile = "9876543212"
		}, TestUserId, DateTime.UtcNow, "9876543212", "mother@example.com");
		AdmissionFormBindingRegistry.GetValue("Guardian.FirstName", applicant, father).ShouldBe("Meera");
		AdmissionFormBindingRegistry.GetValue("Guardian.LastName", applicant, father).ShouldBe("Patel");
		AdmissionFormBindingRegistry.GetValue("Guardian.Mobile", applicant, father).ShouldBe("9876543212");
		AdmissionFormBindingRegistry.GetValue("Guardian.Email", applicant, father).ShouldBe("mother@example.com");
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe("father@example.com");
		father.FirstName.ShouldBe("Ravi"); father.Email.ShouldBe("father@example.com"); father.Mobile.ShouldBe("9876543210");
		return Task.CompletedTask;
	}

	[Fact]
	public Task Sibling_parent_matching_finds_selective_parent_among_more_than_2000_pending_applicants() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var input = SpecialRegistrationInput(offering.Id); input.ParentRelationship = "father"; input.GuardianEmail = "parent@example.com";
		var first = await _applicationAppService.StartDirectRegistrationAsync(input);
		Guid selectedFamilyId = Guid.Empty;
		await WithUnitOfWorkAsync(async () =>
		{
			var applicants = new List<AdmissionApplicant>();
			var inquiries = new List<AdmissionInquiry>();
			var families = new List<AdmissionFamily>();
			for (var index = 0; index < 2000; index++)
			{
				var family = new AdmissionFamily(Guid.NewGuid(), TestTenantId, $"SIB-FAMILY-{index}");
				families.Add(family);
				var pending = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, family.Id, "Pending", null, index.ToString(), null, null, null, Guid.NewGuid());
				pending.SetProperty("AdmissionStudentField.Mother.Email", index == 1999 ? "needle@example.com" : $"unrelated{index}@example.com");
				pending.SetProperty("AdmissionStudentField.Mother.Name", "Meera Patel");
				var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, $"SIB-LIMIT-{index}", offering.AdmissionCycleId,
					offering.Id, "Pending", "9876543210", null, AdmissionInquirySource.WalkIn, null);
				inquiry.LinkIdentity(family.Id, pending.Id);
				if (index == 1999) selectedFamilyId = family.Id;
				applicants.Add(pending); inquiries.Add(inquiry);
			}
			await _familyRepository.InsertManyAsync(families, true);
			await _applicantRepository.InsertManyAsync(applicants, true);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertManyAsync(inquiries, true);
		});
		var match = (await _applicationAppService.FindDirectRegistrationFamiliesAsync(offering.Id, "", "needle@example.com")).ShouldHaveSingleItem();
		match.FamilyId.ShouldBe(selectedFamilyId);
		match.Parents.ShouldHaveSingleItem().Email.ShouldBe("needle@example.com");
	});
}
