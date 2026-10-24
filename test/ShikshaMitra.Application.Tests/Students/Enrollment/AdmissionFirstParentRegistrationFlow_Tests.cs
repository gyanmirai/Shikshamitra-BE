using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("father", "Father", false)]
	[InlineData("mother", "Mother", true)]
	public Task First_parent_inquiry_without_email_registration_alias_survives_accepted_student_handoff(string role, string prefix, bool preFixSubmitted) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await AddFirstParentContactFieldAsync(offering.ApplicationFormDefinitionId!.Value, "PARENT_EMAIL", "Guardian.Email");
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering);
		input.ParentRelationship = role; input.GuardianEmail = null; input.ApplicantLastName = "Patel";
		var inquiry = await inquiries.CreateAsync(input);
		AdmissionStudentProfileMapping.Read(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(inquiry.ApplicantId!.Value)),
			prefix + ".Email").ShouldBeNull();
		inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
		await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = application.ConcurrencyStamp,
			Values = [new() { FieldCode = "PARENT_EMAIL", ValueJson = "\"PARENT@EXAMPLE.COM\"" }]
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
			AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email").ShouldBe("parent@example.com");
			if (preFixSubmitted)
			{
				// Reproduce an already-saved pre-fix record without asking staff to reopen its form.
				applicant.ExtraProperties.Remove("AdmissionStudentField." + prefix + ".Email");
				applicant.ExtraProperties.Remove("ParentAccountAliasContact." + prefix + ".Email");
				await _applicantRepository.UpdateAsync(applicant, true);
			}
		});
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		application = await _applicationAppService.GetAsync(application.Id);
		application = await _applicationAppService.ApproveAsync(application.Id, Decision(application, "Parent contact and admission verified"));
		await CompleteCertificationWorkflowAsync(application.Id);
		var preview = await _applicationAppService.GetFinalizationPreviewAsync(application.Id);
		preview.Items.Where(item => !item.Passed && item.Key != "duplicate_resolution").Select(item => item.Detail).ShouldBeEmpty();
		var finalized = await FinalizeAsync(await _applicationAppService.GetAsync(application.Id), true, "first-parent-alias-handoff");
		await WithUnitOfWorkAsync(async () =>
		{
			var student = (await _studentRepository.GetWithDetailsAsync(finalized.StudentId)).ShouldNotBeNull();
			(prefix == "Father" ? student.FamilyDetail?.FatherDetails : student.FamilyDetail?.MotherDetails).ShouldNotBeNull().Email.ShouldBe("parent@example.com");
			student.FamilyDetail?.GuardianDetails.ShouldBeNull();
			AdmissionStudentProfileMapping.Read(await _applicantRepository.GetAsync(application.ApplicantId), prefix + ".Email").ShouldBe("parent@example.com");
		});
	});

	[Theory]
	[InlineData("structured@example.com")]
	[InlineData("")]
	public Task First_parent_structured_inquiry_email_wins_over_later_registration_alias_including_clear(string structuredEmail) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await AddFirstParentContactFieldAsync(offering.InquiryFormDefinitionId!.Value, "FATHER_EMAIL", "Father.Email");
		await AddFirstParentContactFieldAsync(offering.ApplicationFormDefinitionId!.Value, "PARENT_EMAIL", "Guardian.Email");
		await AddFirstParentContactFieldAsync(offering.ApplicationFormDefinitionId!.Value, "FATHER_EMAIL_PREFILL", "Father.Email");
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering); input.ParentRelationship = "father"; input.GuardianEmail = "generic@example.com";
		input.FormValues.Add(new() { FieldCode = "FATHER_EMAIL", ValueJson = JsonSerializer.Serialize(structuredEmail) });
		var inquiry = await inquiries.CreateAsync(input);
		inquiry = await inquiries.UpdateAsync(inquiry.Id, InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested));
		var application = await _applicationAppService.StartAsync(inquiry.ApplicantId!.Value, offering.Id, inquiry.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var previous = await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value);
			previous.ExtraProperties.Remove("ParentAccountStructuredContact.Father.Email");
			previous.ExtraProperties.Remove("ParentAccountAliasContact.Father.Email");
			// Pre-fix CaptureParent used the generic fallback for an explicitly blank structured field.
			if (string.IsNullOrEmpty(structuredEmail))
			{
				previous.SetProperty("AdmissionStudentField.Father.Email", "generic@example.com");
				var instance = await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().GetAsync(item => item.ApplicationId == application.Id);
				await GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
					instance.Id, "FATHER_EMAIL_PREFILL", "\"generic@example.com\""), true);
			}
			await _applicantRepository.UpdateAsync(previous, true);
		});
		await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
		{
			ConcurrencyStamp = application.ConcurrencyStamp, Values = [new() { FieldCode = "PARENT_EMAIL", ValueJson = "\"later@example.com\"" }]
		});
		var applicant = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(application.ApplicantId));
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe(string.IsNullOrEmpty(structuredEmail) ? null : structuredEmail);
	});

	[Theory]
	[InlineData("explicit@example.com", false)]
	[InlineData("", false)]
	[InlineData("explicit@example.com", true)]
	[InlineData("", true)]
	public Task First_parent_pre_fix_structured_inquiry_contact_survives_generic_inquiry_edits(string structuredEmail, bool formEdit) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await AddFirstParentContactFieldAsync(offering.InquiryFormDefinitionId!.Value, "FATHER_EMAIL", "Father.Email");
		await AddFirstParentContactFieldAsync(offering.InquiryFormDefinitionId!.Value, "PARENT_EMAIL", "Guardian.Email");
		var inquiries = GetRequiredService<IAdmissionInquiryAppService>();
		var input = ManagedInquiryInput(offering); input.ParentRelationship = "father"; input.GuardianEmail = "generic@example.com";
		input.FormValues.Add(new() { FieldCode = "FATHER_EMAIL", ValueJson = JsonSerializer.Serialize(structuredEmail) });
		var inquiry = await inquiries.CreateAsync(input);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(inquiry.ApplicantId!.Value);
			applicant.ExtraProperties.Remove("ParentAccountStructuredContact.Father.Email");
			applicant.ExtraProperties.Remove("ParentAccountAliasContact.Father.Email");
			if (string.IsNullOrEmpty(structuredEmail)) applicant.SetProperty("AdmissionStudentField.Father.Email", "generic@example.com");
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var update = InquiryStatusUpdate(inquiry, AdmissionInquiryStatus.Interested);
		update.Email = "updated-generic@example.com";
		if (formEdit)
		{
			update.FormValues = input.FormValues.Where(value => value.FieldCode != "FATHER_EMAIL").ToList();
			update.FormValues.Add(new() { FieldCode = "PARENT_EMAIL", ValueJson = "\"updated-generic@example.com\"" });
		}
		await inquiries.UpdateAsync(inquiry.Id, update);
		var saved = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(inquiry.ApplicantId!.Value));
		AdmissionStudentProfileMapping.Read(saved, "Father.Email").ShouldBe(string.IsNullOrEmpty(structuredEmail) ? null : structuredEmail);
	});

	[Fact]
	public Task First_parent_registration_generic_email_can_be_corrected_then_explicitly_cleared() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateSpecialRegistrationOfferingAsync();
		await AddFirstParentContactFieldAsync(offering.ApplicationFormDefinitionId!.Value, "PARENT_EMAIL", "Guardian.Email");
		var input = SpecialRegistrationInput(offering.Id); input.ParentRelationship = "mother";
		var application = await _applicationAppService.StartDirectRegistrationAsync(input);
		foreach (var email in new[] { "first@example.com", "corrected@example.com", "" })
		{
			application = await _applicationAppService.GetAsync(application.Id);
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = application.ConcurrencyStamp, Values = [new() { FieldCode = "PARENT_EMAIL", ValueJson = JsonSerializer.Serialize(email) }]
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
				AdmissionStudentProfileMapping.Read(applicant, "Mother.Email").ShouldBe(string.IsNullOrEmpty(email) ? null : email);
				var family = await _familyRepository.GetAsync(applicant.FamilyId);
				(await _guardianRepository.GetAsync(family.PrimaryGuardianId!.Value)).Email.ShouldBe(string.IsNullOrEmpty(email) ? null : email);
			});
		}
	});

	private Task AddFirstParentContactFieldAsync(Guid formId, string code, string bindingKey) => WithUnitOfWorkAsync(async () =>
	{
		var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == formId)).First();
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id,
			code, "Parent email", AdmissionFormFieldType.Email, AdmissionFormBindingType.System, bindingKey,
			false, false, 98, null, null, null, null, null), true);
	});
}
