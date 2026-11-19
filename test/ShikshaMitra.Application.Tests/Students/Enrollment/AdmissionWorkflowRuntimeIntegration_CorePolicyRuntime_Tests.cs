using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Core_policy_runtime_inquiry_requires_parent_email_on_create_and_edit_and_accepts_single_name() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePublishedCorePolicyRouteAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		foreach (var email in new[] { "", "invalid-email" })
		{
			var invalid = CorePolicyInquiryInput(fixture.Offering, fixture.Inquiry, email);
			(await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(invalid))).Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
		}
		var created = await service.CreateAsync(CorePolicyInquiryInput(fixture.Offering, fixture.Inquiry, "parent@example.com"));
		var detail = await service.GetAsync(created.Id);
		detail.ApplicantFirstName.ShouldBe("Mira");
		detail.ApplicantLastName.ShouldBeNullOrWhiteSpace();
		detail.ParentRelationship.ShouldBe("father");
		detail.Email.ShouldBe("parent@example.com");
		detail.FormDefinition!.CoreFieldPolicyVersion.ShouldBe(1);
		var sourceSnapshot = await ReadInheritedInquirySnapshotAsync(detail.Id);
		foreach (var fullForm in new[] { true, false })
		{
			var invalid = CorePolicyInquiryEdit(detail, fullForm ? CorePolicyInquiryAnswers(fixture.Inquiry, "") : null);
			invalid.Email = null;
			(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(detail.Id, invalid))).Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
		}
		(await ReadInheritedInquirySnapshotAsync(detail.Id)).ShouldBe(sourceSnapshot);
		var updated = await service.UpdateAsync(detail.Id, CorePolicyInquiryEdit(detail, CorePolicyInquiryAnswers(fixture.Inquiry, "updated@example.com")));
		updated.Email.ShouldBe("updated@example.com");
		updated.ApplicantLastName.ShouldBeNullOrWhiteSpace();
		updated.ParentRelationship.ShouldBe("father");
		(await service.GetAsync(detail.Id)).Email.ShouldBe("updated@example.com");
	});

	[Fact]
	public Task Core_policy_runtime_direct_registration_requires_parent_email_and_accepts_single_name_and_replay() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePublishedCorePolicyRouteAsync(direct: true);
		var input = SpecialRegistrationInput(fixture.Offering.Id);
		input.ApplicantLastName = null;
		input.GuardianLastName = null;
		input.ParentRelationship = "father";
		input.GuardianEmail = null;
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.StartDirectRegistrationAsync(input))).Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
		await WithUnitOfWorkAsync(async () => (await _applicantRepository.FindAsync(input.RequestId)).ShouldBeNull());
		input.GuardianEmail = "invalid-email";
		await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(() => _applicationAppService.StartDirectRegistrationAsync(input));
		await WithUnitOfWorkAsync(async () => (await _applicantRepository.FindAsync(input.RequestId)).ShouldBeNull());
		input.GuardianEmail = "parent@example.com";
		var application = await _applicationAppService.StartDirectRegistrationAsync(input);
		(await _applicationAppService.StartDirectRegistrationAsync(input)).Id.ShouldBe(application.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
			applicant.LastName.ShouldBeNullOrWhiteSpace();
			AdmissionPrimaryContactSelection.ReadRelationship(applicant).ShouldBe("father");
			var family = await _familyRepository.GetAsync(applicant.FamilyId);
			(await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value)).Email.ShouldBe("parent@example.com");
		});
	});

	[Fact]
	public Task Core_policy_runtime_reuse_freezes_source_and_keeps_relationship_as_immutable_context() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreatePublishedCorePolicyRouteAsync();
		var inquiryService = GetRequiredService<IAdmissionInquiryAppService>();
		var inquiry = await inquiryService.CreateAsync(CorePolicyInquiryInput(fixture.Offering, fixture.Inquiry, "parent@example.com"));
		await MarkInheritedInquiryInterestedAsync(inquiry.Id);
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, fixture.Offering.Id, inquiry.Id);
		var form = await _applicationAppService.GetFormAsync(application.Id);
		var relationship = form.InquiryContextSections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Inquiry.ParentRelationship");
		relationship.ReadOnly.ShouldBeTrue();
		relationship.InheritedFrom!.InquiryFormDefinitionId.ShouldBe(fixture.Inquiry.Id);
		form.InquiryContextValues.Single(value => value.FieldCode == relationship.Code).ValueJson.ShouldBe(JsonSerializer.Serialize("father"));
		form.Definition.Sections.SelectMany(section => section.Fields).ShouldNotContain(field => field.BindingKey == "Inquiry.ParentRelationship");
		var frozen = JsonSerializer.Serialize(form.Definition);
		var sourceSnapshot = await ReadInheritedInquirySnapshotAsync(inquiry.Id);
		var concurrency = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp;
		(await Should.ThrowAsync<BusinessException>(() => _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = concurrency,
			Values = [new() { FieldCode = relationship.Code, ValueJson = JsonSerializer.Serialize("mother") }]
		}))).Code.ShouldBe(AdmissionApplicationErrorCodes.UnknownFormField);
		var version = await _formAppService.CreateNewVersionAsync(fixture.Inquiry.Id);
		var edit = JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(JsonSerializer.Serialize(version))!;
		edit.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Inquiry.ParentRelationship").Label = "Updated relationship label";
		version = await _formAppService.UpdateAsync(version.Id, edit);
		await _formAppService.PublishAsync(version.Id, version.ConcurrencyStamp);
		var reopened = await _applicationAppService.GetFormAsync(application.Id);
		JsonSerializer.Serialize(reopened.Definition).ShouldBe(frozen);
		reopened.InquiryContextSections.SelectMany(section => section.Fields).Single(field => field.Code == relationship.Code).Label.ShouldBe("Relationship to student");
		reopened.InquiryContextValues.Single(value => value.FieldCode == relationship.Code).ValueJson.ShouldBe(JsonSerializer.Serialize("father"));
		(await ReadInheritedInquirySnapshotAsync(inquiry.Id)).ShouldBe(sourceSnapshot);
		(await inquiryService.GetAsync(inquiry.Id)).ParentRelationship.ShouldBe("father");
	});

	private async Task<(AdmissionOffering Offering, AdmissionFormDto Inquiry, AdmissionFormDto Registration)> CreatePublishedCorePolicyRouteAsync(bool direct = false)
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var inquiry = await _formAppService.CreateAsync(new() { Name = "Core runtime inquiry", Purpose = AdmissionFormPurpose.Inquiry });
		inquiry = await _formAppService.PublishAsync(inquiry.Id, inquiry.ConcurrencyStamp);
		var registration = await _formAppService.CreateAsync(new()
		{
			Name = "Core runtime registration", Purpose = AdmissionFormPurpose.Registration,
			Inheritance = direct ? null : new() { Enabled = true, Sources = [new() { InquiryFormDefinitionId = inquiry.Id }] }
		});
		registration = await _formAppService.PublishAsync(registration.Id, registration.ConcurrencyStamp);
		var workflow = await CreateDirectWorkflowDraftAsync();
		var workflowInput = ToUpdateInput(workflow);
		workflowInput.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson = JsonSerializer.Serialize(new { formDefinitionId = registration.Id });
		workflow = await _workflowAppService.UpdateAsync(workflow.Id, workflowInput);
		workflow = await _workflowAppService.PublishAsync(workflow.Id, workflow.ConcurrencyStamp);
		offering = await WithUnitOfWorkAsync(async () =>
		{
			var stored = await _offeringRepository.GetAsync(offering.Id);
			if (direct) stored.UpdateRouting(AdmissionTrack.Rte, AdmissionEntryMode.DirectRegistration, false);
			stored.ConfigureReferences(workflow.Id, direct ? null : inquiry.Id, registration.Id);
			await _offeringRepository.UpdateAsync(stored, true);
			(await _offeringValidator.ValidateStoredAsync(stored)).Errors.ShouldBeEmpty();
			return stored;
		});
		return (offering, inquiry, registration);
	}

	private static CreateAdmissionInquiryDto CorePolicyInquiryInput(AdmissionOffering offering, AdmissionFormDto form, string email) => new()
	{
		AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
		BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
		Source = AdmissionInquirySource.WalkIn, FormValues = CorePolicyInquiryAnswers(form, email)
	};

	private static List<AdmissionFormValueDto> CorePolicyInquiryAnswers(AdmissionFormDto form, string email) => form.Sections.SelectMany(section => section.Fields).Select(field => new AdmissionFormValueDto
	{
		FieldCode = field.Code,
		ValueJson = JsonSerializer.Serialize(field.BindingKey switch
		{
			"Applicant.FirstName" => "Mira", "Applicant.LastName" => null,
			"Guardian.FirstName" => "Amit", "Guardian.Mobile" => "9876543210",
			"Guardian.Email" => email, "Inquiry.ParentRelationship" => "father", _ => null
		})
	}).ToList();

	private static UpdateAdmissionInquiryDto CorePolicyInquiryEdit(AdmissionInquiryDto detail, List<AdmissionFormValueDto>? answers) => new()
	{
		ApplicantName = detail.ApplicantName, Phone = detail.NormalizedPhone, Email = detail.Email,
		Source = detail.Source, PreferredLanguageCode = detail.PreferredLanguageCode,
		ConcurrencyStamp = detail.ConcurrencyStamp, FormValues = answers
	};
}
