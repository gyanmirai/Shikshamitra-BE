using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Hidden_canonical_parent_email_does_not_clear_visible_generic_email_on_create_or_edit() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreatePrimaryContactInquiryOfferingAsync();
		var fields = GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>();
		var section = (await fields.GetListAsync(field => field.FormDefinitionId == offering.InquiryFormDefinitionId)).First().SectionId;
		await fields.InsertAsync(new(Guid.NewGuid(), offering.InquiryFormDefinitionId!.Value, section, "FATHER_EMAIL", "Other parent email",
			AdmissionFormFieldType.Email, AdmissionFormBindingType.System, "Father.Email", false, false, 5, null, null, null, null,
			"{\"field\":\"PRIMARY_CONTACT\",\"operator\":\"notEquals\",\"value\":\"father\"}"), true);
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var input = PrimaryContactInquiryInput(offering, "father");
		var created = await service.CreateAsync(input);
		var detail = await service.GetAsync(created.Id);
		detail.Email.ShouldBe("amit@example.com");
		var edited = await service.UpdateAsync(created.Id, new UpdateAdmissionInquiryDto
		{
			ApplicantName = detail.ApplicantName, Phone = detail.NormalizedPhone, Email = detail.Email,
			Source = detail.Source, PreferredLanguageCode = detail.PreferredLanguageCode, ConcurrencyStamp = detail.ConcurrencyStamp,
			FormValues = input.FormValues
		});
		edited.Email.ShouldBe("amit@example.com");
		AdmissionStudentProfileMapping.Read(await _applicantRepository.GetAsync(detail.ApplicantId!.Value), "Father.Email").ShouldBe("amit@example.com");
	});
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task New_direct_registration_guardian_main_contact_requires_school_responsibility_confirmation(bool alternateFamilyContact) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		var input = SpecialRegistrationInput(offering.Id);
		if (alternateFamilyContact)
		{
			input.ParentRelationship = "father";
			input.FamilyContacts = new() { MainContactRelationship = "guardian", OtherGuardian = new() { FullName = "Anita", Relationship = "Aunt", Mobile = "9876543212" } };
		}
		else input.GuardianRelationship = AdmissionGuardianRelationship.Guardian;
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(input)))
			.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	});
	[Theory]
	[InlineData("father")]
	[InlineData("mother")]
	public Task Configured_public_primary_parent_survives_in_generic_aliases_without_sibling_authority(string role) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreatePrimaryContactInquiryOfferingAsync();
		var input = PrimaryContactInquiryInput(offering, role);
		var result = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAppService>().CreateFromPublicLinkAsync(input, null));
		var saved = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(result.ApplicantId!.Value));
		AdmissionPrimaryContactSelection.ReadRelationship(saved).ShouldBe(role);
		AdmissionStudentProfileMapping.Read(saved, (role == "father" ? "Father" : "Mother") + ".Name").ShouldBe("Amit Shah");
		AdmissionSiblingParentSelection.ReadConfirmation(saved).ShouldBeNull();
	});

	[Fact]
	public Task Public_guardian_remains_pending_until_authenticated_staff_confirms_on_edit() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreatePrimaryContactInquiryOfferingAsync();
		var input = PrimaryContactInquiryInput(offering, "guardian");
		input.GuardianResponsibilityConfirmed = true; // Anonymous input is never trusted.
		var result = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionInquiryAppService>().CreateFromPublicLinkAsync(input, null));
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var detail = await service.GetAsync(result.Id);
		detail.PrimaryContactRelationship.ShouldBe("guardian");
		detail.ParentRelationship.ShouldBeNull();
		detail.GuardianResponsibilityConfirmed.ShouldBeFalse();
		await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
		{
			var inquiry = await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetAsync(result.Id);
			await GetRequiredService<AdmissionInquiryRuntimeService>().UpdateStatusAsync(inquiry, AdmissionInquiryStatus.Interested, null);
		}));
		var updated = await service.UpdateAsync(result.Id, new UpdateAdmissionInquiryDto
		{
			ApplicantName = detail.ApplicantName, Phone = detail.NormalizedPhone, Email = detail.Email,
			Source = detail.Source, PreferredLanguageCode = detail.PreferredLanguageCode, ConcurrencyStamp = detail.ConcurrencyStamp,
			GuardianResponsibilityConfirmed = true, FormValues = input.FormValues
		});
		detail = await service.GetAsync(updated.Id);
		detail.GuardianResponsibilityConfirmed.ShouldBeTrue();
		AdmissionPrimaryContactSelection.EnsureReady(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(detail.ApplicantId!.Value)));
	});

	[Fact]
	public Task Staff_guardian_capture_requires_the_explicit_responsibility_confirmation() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreatePrimaryContactInquiryOfferingAsync();
		var input = PrimaryContactInquiryInput(offering, "guardian");
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(input)))
			.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	});

	private async Task<AdmissionOffering> CreatePrimaryContactInquiryOfferingAsync()
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(genericContact: true);
		var fields = GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>();
		var section = (await fields.GetListAsync(field => field.FormDefinitionId == offering.InquiryFormDefinitionId)).First().SectionId;
		await fields.InsertAsync(new(Guid.NewGuid(), offering.InquiryFormDefinitionId!.Value, section, "PRIMARY_CONTACT", "Primary contact",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.System, "Inquiry.ParentRelationship", true, false, 4, null, null, null, null, null,
			"[{\"value\":\"father\",\"label\":\"Father\"},{\"value\":\"mother\",\"label\":\"Mother\"},{\"value\":\"guardian\",\"label\":\"Guardian\"}]"), true);
		return offering;
	}

	private static CreateAdmissionInquiryDto PrimaryContactInquiryInput(AdmissionOffering offering, string role) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
		BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
		FormValues = [.. ConfiguredInquiryAnswers(), new() { FieldCode = "PRIMARY_CONTACT", ValueJson = JsonSerializer.Serialize(role) }]
	};
}
