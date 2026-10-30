using System;
using System.Collections.Generic;
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
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Existing_published_optional_inquiry_identity_fields_support_create_and_edit(bool genericContact) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(genericContact: genericContact, requiredIdentity: false);
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var created = await service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = ConfiguredInquiryAnswers()
		});
		created = await service.GetAsync(created.Id);
		created.ApplicantName.ShouldBe("Riya Shah");
		created.GuardianFirstName.ShouldBe("Amit Shah");
		created.NormalizedPhone.ShouldBe("9876543210");
		created.FormDefinition!.Sections.SelectMany(section => section.Fields).ShouldAllBe(field => !field.Required);
		var updated = await service.UpdateAsync(created.Id, new UpdateAdmissionInquiryDto
		{
			ApplicantName = created.ApplicantName, Phone = created.NormalizedPhone, Email = created.Email,
			Source = created.Source, PreferredLanguageCode = created.PreferredLanguageCode,
			ConcurrencyStamp = created.ConcurrencyStamp, FormValues = ConfiguredInquiryAnswers("Meera Shah", "9876543211")
		});
		updated.GuardianFirstName.ShouldBe("Meera Shah");
		updated.NormalizedPhone.ShouldBe("9876543211");
	});

	[Fact]
	public Task Configured_inquiry_creates_and_edits_using_only_published_father_fields() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var input = new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = ConfiguredInquiryAnswers()
		};
		var created = await service.CreateAsync(input);
		created = await service.GetAsync(created.Id);
		created.ApplicantName.ShouldBe("Riya Shah");
		created.GuardianFirstName.ShouldBe("Amit Shah");
		created.NormalizedPhone.ShouldBe("9876543210");
		created.ParentRelationship.ShouldBe("father");
		created.Source.ShouldBe(AdmissionInquirySource.WalkIn);
		created.FormDefinition!.Sections.SelectMany(section => section.Fields).Select(field => field.Code)
			.ShouldBe(new[] { "CHILD", "ADULT", "PHONE", "EMAIL" }, ignoreOrder: true);
		var answers = ConfiguredInquiryAnswers("Neha Shah", "9876543211", "new@example.com");
		var updated = await service.UpdateAsync(created.Id, new UpdateAdmissionInquiryDto
		{
			ApplicantName = created.ApplicantName, Phone = created.NormalizedPhone, Email = created.Email,
			Source = created.Source, PreferredLanguageCode = created.PreferredLanguageCode,
			ConcurrencyStamp = created.ConcurrencyStamp, FormValues = answers
		});
		updated.GuardianFirstName.ShouldBe("Neha Shah");
		updated.NormalizedPhone.ShouldBe("9876543211");
		updated.Email.ShouldBe("new@example.com");
		updated.ParentRelationship.ShouldBe("father");
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Configured_inquiry_edit_preserves_hidden_readonly_primary_email_without_allowing_explicit_changes(bool genericContact) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(genericContact: genericContact, hiddenReadonlyEmail: true);
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var answers = ConfiguredInquiryAnswers();
		answers.Add(new() { FieldCode = "SHOW_EMAIL", ValueJson = "\"yes\"" });
		var created = await service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId, FormValues = answers
		});
		created = await service.GetAsync(created.Id);
		answers = ConfiguredInquiryAnswers("Updated parent").Where(answer => answer.FieldCode != "EMAIL").ToList();
		answers.Add(new() { FieldCode = "SHOW_EMAIL", ValueJson = "\"no\"" });
		var updated = await service.UpdateAsync(created.Id, new()
		{
			ApplicantName = created.ApplicantName, Phone = created.NormalizedPhone, Email = created.Email,
			Source = created.Source, PreferredLanguageCode = created.PreferredLanguageCode, ConcurrencyStamp = created.ConcurrencyStamp, FormValues = answers
		});
		updated.Email.ShouldBe("amit@example.com");
		updated.FormValues.Single(answer => answer.FieldCode == "EMAIL").ValueJson.ShouldBe("\"amit@example.com\"");
		answers.Add(new() { FieldCode = "EMAIL", ValueJson = "\"\"" });
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(created.Id, new()
		{
			ApplicantName = updated.ApplicantName, Phone = updated.NormalizedPhone, Email = updated.Email,
			Source = updated.Source, PreferredLanguageCode = updated.PreferredLanguageCode, ConcurrencyStamp = updated.ConcurrencyStamp, FormValues = answers
		}))).Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	});

	[Theory]
	[InlineData(false, "Neha Shah", null)]
	[InlineData(true, "Neha", "Old surname")]
	public Task Configured_inquiry_edit_preserves_unconfigured_email_and_omitted_hidden_readonly_answers(bool genericContact, string name, string? expectedLastName) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(includeEmail: false, genericContact: genericContact);
		await WithUnitOfWorkAsync(async () =>
		{
			var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), offering.InquiryFormDefinitionId!.Value, "EXTRA", "Extra", 1, null,
				stepVisibilityRuleJson: "{\"field\":\"SHOW_EXTRA\",\"operator\":\"equals\",\"value\":\"yes\"}");
			await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(section, false);
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), section.FormDefinitionId, section.Id, "LOCKED", "Saved detail", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, "Custom.Locked", true, true, 0, null, null, "saved value", null, null), true);
			var main = (await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().GetListAsync(item => item.FormDefinitionId == offering.InquiryFormDefinitionId && item.Code == "INFORMATION")).Single();
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), main.FormDefinitionId, main.Id, "SHOW_EXTRA", "Extra details", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, "Custom.ShowExtra", false, false, 9, null, null, null, null, null), true);
		});
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var answers = ConfiguredInquiryAnswers().Where(answer => answer.FieldCode != "EMAIL").ToList();
		answers.Add(new() { FieldCode = "SHOW_EXTRA", ValueJson = "\"yes\"" });
		answers.Add(new() { FieldCode = "LOCKED", ValueJson = "\"saved value\"" });
		var created = await service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId, FormValues = answers
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
			var current = await repository.GetAsync(created.Id);
			current.UpdateDetails(current.ApplicantName, current.NormalizedPhone, "saved@example.com", current.Source, current.SourceDetail, current.PreferredLanguageCode);
			await repository.UpdateAsync(current, true);
			var family = await GetRequiredService<IRepository<AdmissionFamily, Guid>>().GetAsync(current.FamilyId!.Value);
			var guardian = await GetRequiredService<IRepository<AdmissionGuardian, Guid>>().GetAsync(family.PrimaryGuardianId!.Value);
			guardian.UpdateContact(guardian.FirstName, "Old surname", guardian.Mobile, "saved@example.com");
			await GetRequiredService<IRepository<AdmissionGuardian, Guid>>().UpdateAsync(guardian, true);
		});
		created = await service.GetAsync(created.Id);
		answers = ConfiguredInquiryAnswers(name).Where(answer => answer.FieldCode != "EMAIL").ToList();
		answers.Add(new() { FieldCode = "SHOW_EXTRA", ValueJson = "\"no\"" });
		var updated = await service.UpdateAsync(created.Id, new()
		{
			ApplicantName = created.ApplicantName, Phone = created.NormalizedPhone, Email = created.Email,
			Source = created.Source, PreferredLanguageCode = created.PreferredLanguageCode, ConcurrencyStamp = created.ConcurrencyStamp, FormValues = answers
		});
		updated.Email.ShouldBe("saved@example.com");
		updated.GuardianLastName.ShouldBe(expectedLastName);
		updated.FormValues.Single(answer => answer.FieldCode == "LOCKED").ValueJson.ShouldBe("\"saved value\"");
	});

	[Fact]
	public Task Configured_father_form_rejects_forged_mother_sibling_selection() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		var first = await service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = ConfiguredInquiryAnswers()
		});
		var error = await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
			Source = AdmissionInquirySource.WalkIn, FormValues = ConfiguredInquiryAnswers(),
			ExistingFamilyId = first.FamilyId, ExistingParentApplicantId = first.ApplicantId, ParentRelationship = "mother"
		}));
		error.Code.ShouldBe(AdmissionInquiryManagementErrorCodes.InvalidInput);
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Configured_inquiry_sibling_preserves_verified_parent_email_only_when_email_question_is_absent(bool includeEmail) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync(includeEmail);
		var service = GetRequiredService<IAdmissionInquiryAppService>();
		List<AdmissionFormValueDto> Answers() => ConfiguredInquiryAnswers(email: "").Where(answer => includeEmail || answer.FieldCode != "EMAIL").ToList();
		var first = await service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId, FormValues = Answers()
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var applicants = GetRequiredService<IRepository<AdmissionApplicant, Guid>>();
			var parent = await applicants.GetAsync(first.ApplicantId!.Value);
			AdmissionSiblingParentSelection.CaptureParent(parent, "father", "Amit Shah", "9876543210", "saved@example.com");
			await applicants.UpdateAsync(parent, true);
		});
		var answers = Answers();
		answers.Single(answer => answer.FieldCode == "CHILD").ValueJson = "\"Meera Shah\"";
		var second = await service.CreateAsync(new CreateAdmissionInquiryDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, AdmissionOfferingId = offering.Id,
			BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId, FormValues = answers,
			ExistingFamilyId = first.FamilyId, ExistingParentApplicantId = first.ApplicantId, ParentRelationship = "father"
		});
		second = await service.GetAsync(second.Id);
		second.Email.ShouldBe(includeEmail ? null : "saved@example.com");
	});

	[Fact]
	public Task Public_configured_inquiry_has_no_generated_questions_and_keeps_link_attribution() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateConfiguredInquiryOfferingAsync();
		var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
		var link = await service.CreateLinkAsync(new CreateAdmissionPublicInquiryLinkDto
		{
			AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
			Name = "Configured intake", Source = AdmissionInquirySource.QrCode, SourceDetail = "Published QR"
		});
		var context = await service.GetPublicAsync(link.Token!, offering.Id);
		context.Form!.Sections.SelectMany(section => section.Fields).ShouldAllBe(field => !field.IsIdentityFallback);
		context.Form.Sections.SelectMany(section => section.Fields).Count().ShouldBe(4);
		context.Form.Sections.SelectMany(section => section.Fields).Single(field => field.Code == "ADULT").BindingKey.ShouldBe("Father.Name");
		await service.SubmitPublicAsync(link.Token!, new SubmitPublicAdmissionInquiryDto
		{
			AdmissionOfferingId = offering.Id, BoardId = offering.BoardId, MediumId = offering.MediumId, StandardId = offering.StandardId,
			ConsentToContact = true, FormValues = ConfiguredInquiryAnswers()
		});
		var inquiry = (await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().GetListAsync(item => item.AdmissionOfferingId == offering.Id)).Single();
		inquiry.Source.ShouldBe(AdmissionInquirySource.QrCode);
		inquiry.SourceDetail.ShouldBe("Published QR");
		inquiry.NormalizedPhone.ShouldBe("9876543210");
		var applicant = await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value);
		AdmissionSiblingParentSelection.HasParentFlowProvenance(applicant).ShouldBeFalse();
		AdmissionStudentProfileMapping.Read(applicant, "Father.Name").ShouldBe("Amit Shah");
	});

	[Fact]
	public Task Inquiry_publication_rejects_missing_minimum_contact_without_modifying_draft() => RunCertificationScenarioAsync(async () =>
	{
		var forms = GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>();
		var form = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Incomplete inquiry", $"MINIMUM_{Guid.NewGuid():N}"[..25], AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
		var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "DETAILS", "Details", 0, null);
		await forms.InsertAsync(form, false);
		await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(section, false);
		await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), form.Id, section.Id, "CHILD", "Student name", AdmissionFormFieldType.Text, AdmissionFormBindingType.System, "Applicant.Name", true, false, 0, null, null, null, null, null), true);
		var service = GetRequiredService<IAdmissionFormAppService>();
		var result = await service.ValidateAsync(form.Id);
		result.Errors.ShouldContain("Add a required Parent / Guardian name field before publishing.");
		result.Errors.ShouldContain("Add a required Parent / Guardian mobile field before publishing.");
		var current = await service.GetAsync(form.Id);
		(await Should.ThrowAsync<BusinessException>(() => service.PublishAsync(form.Id, current.ConcurrencyStamp))).Code.ShouldBe(AdmissionFormErrorCodes.InvalidDefinition);
		(await forms.GetAsync(form.Id)).Status.ShouldBe(AdmissionFormStatus.Draft);
	});

	private async Task<AdmissionOffering> CreateConfiguredInquiryOfferingAsync(bool includeEmail = true, bool genericContact = false, bool hiddenReadonlyEmail = false, bool requiredIdentity = true)
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var form = new AdmissionFormDefinition(Guid.NewGuid(), TestTenantId, "Configured inquiry", $"CONFIGURED_{Guid.NewGuid():N}"[..25], AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
		var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), form.Id, "INFORMATION", "Inquiry Information", 0, null);
		form.Publish(TestUserId);
		await GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(form, false);
		await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(section, false);
		var definitions = new[]
		{
			("CHILD", "Student name", "Applicant.Name", AdmissionFormFieldType.Text, true),
			("ADULT", "Father's name", "Father.Name", AdmissionFormFieldType.Text, true),
			("PHONE", "Father's mobile", "Father.Mobile", AdmissionFormFieldType.Phone, true),
			("EMAIL", "Parent email", "Father.Email", AdmissionFormFieldType.Email, false)
		};
		foreach (var (code, label, binding, type, required) in definitions)
		{
			if (!includeEmail && code == "EMAIL") continue;
			var key = genericContact ? binding switch { "Father.Name" => "Guardian.FirstName", "Father.Mobile" => "Guardian.Mobile", "Father.Email" => "Guardian.Email", _ => binding } : binding;
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), form.Id, section.Id, code, label, type, AdmissionFormBindingType.System, key, required && requiredIdentity, hiddenReadonlyEmail && code == "EMAIL", 0, null, null, null, null,
				hiddenReadonlyEmail && code == "EMAIL" ? "{\"field\":\"SHOW_EMAIL\",\"operator\":\"equals\",\"value\":\"yes\"}" : null), false);
		}
		if (hiddenReadonlyEmail)
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), form.Id, section.Id, "SHOW_EMAIL", "Show email", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, null, false, false, 5, null, null, null, null, null), false);
		return await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<AdmissionOffering, Guid>>();
			var current = await repository.GetAsync(offering.Id);
			current.ConfigureReferences(current.WorkflowDefinitionId, form.Id, current.ApplicationFormDefinitionId);
			return await repository.UpdateAsync(current, true);
		});
	}

	private static List<AdmissionFormValueDto> ConfiguredInquiryAnswers(string parent = "Amit Shah", string mobile = "9876543210", string email = "amit@example.com") =>
	[
		new() { FieldCode = "CHILD", ValueJson = JsonSerializer.Serialize("Riya Shah") },
		new() { FieldCode = "ADULT", ValueJson = JsonSerializer.Serialize(parent) },
		new() { FieldCode = "PHONE", ValueJson = JsonSerializer.Serialize(mobile) },
		new() { FieldCode = "EMAIL", ValueJson = JsonSerializer.Serialize(email) }
	];
}
