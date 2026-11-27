using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Form_audit_resolves_recorded_staff_without_guessing_missing_actors() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var id = offering.InquiryFormDefinitionId!.Value;
		var actorId = Guid.NewGuid();
		var unknownActorId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var userName = $"audit-{actorId:N}";
			var actor = new IdentityUser(actorId, userName, $"{userName}@example.test", TestTenantId)
			{
				Name = "Audit",
				Surname = "Reviewer"
			};
			(await GetRequiredService<IdentityUserManager>().CreateAsync(actor)).Succeeded.ShouldBeTrue();
			var forms = GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>();
			var form = await forms.GetAsync(id);
			AdmissionFormMetadataStore.AddAudit(form, "Published", actorId);
			AdmissionFormMetadataStore.AddAudit(form, "Archived", unknownActorId);
			AdmissionFormMetadataStore.AddAudit(form, "SystemEvent", null);
			await forms.UpdateAsync(form, autoSave: true);
		});

		var entries = await GetRequiredService<IAdmissionFormAppService>().GetAuditAsync(id);
		entries.Single(entry => entry.ActorId == actorId).ActorName.ShouldBe("Audit Reviewer");
		entries.Single(entry => entry.ActorId == unknownActorId).ActorName.ShouldBeNull();
		entries.Single(entry => entry.Action == "SystemEvent").ActorName.ShouldBeNull();
	});

	[Fact]
	public Task Form_archive_restore_retains_ten_saved_inquiries_and_all_links() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		await PrepareRestorableInquiryFormAsync(offering);
		var service = GetRequiredService<IAdmissionFormAppService>();
		var id = offering.InquiryFormDefinitionId!.Value;
		var before = await service.GetAsync(id);
		(await service.ValidateAsync(id)).Errors.ShouldBeEmpty();
		var inquiries = GetRequiredService<IRepository<AdmissionInquiry, Guid>>();
		await WithUnitOfWorkAsync(async () =>
		{
			for (var index = 0; index < 10; index++)
			{
				var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, $"ARCHIVE-{index}-{Guid.NewGuid():N}",
					offering.AdmissionCycleId, offering.Id, $"Saved student {index}", "9876543210", null, AdmissionInquirySource.WalkIn, null);
				inquiry.RecordFormResponseSnapshot(JsonSerializer.Serialize(new { SavedAnswer = index }), id);
				await inquiries.InsertAsync(inquiry, autoSave: false);
			}
		});
		var publicLink = new AdmissionPublicInquiryLink(Guid.NewGuid(), TestTenantId, offering.AdmissionCycleId,
			null, "Saved public inquiry link", new string('a', 64), AdmissionInquirySource.Website, null, null, id);
		await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().InsertAsync(publicLink, autoSave: true);
		var savedSnapshots = (await inquiries.GetListAsync(item => item.AdmissionOfferingId == offering.Id))
			.ToDictionary(item => item.Id, item => item.FormResponseSnapshotJson);
		var preview = await service.GetArchiveImpactAsync(id);
		preview.InquiryCount.ShouldBe(10);
		preview.Dependencies.ShouldContain(item => item.Kind == "offering" && item.Id == offering.Id);
		preview.PublicLinkCount.ShouldBe(1);
		preview.Dependencies.ShouldContain(item => item.Kind == "publicLink" && item.Id == publicLink.Id);
		var archived = await service.ArchiveAsync(id, preview.ConcurrencyStamp);
		archived.Status.ShouldBe(AdmissionFormStatus.Archived);
		var offeringDetail = await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(offering.Id);
		var blocked = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IAdmissionOfferingAppService>()
			.ActivateAsync(offering.Id, offeringDetail.ConcurrencyStamp));
		blocked.Code.ShouldBe(AdmissionErrorCodes.OfferingFormNotPublished);
		blocked.Data["ConfigurationErrors"].ShouldNotBeNull();
		await WithUnitOfWorkAsync(async () =>
		{
			var stored = await inquiries.GetListAsync(item => item.AdmissionOfferingId == offering.Id);
			stored.Count.ShouldBe(10);
			stored.ShouldAllBe(item => item.FormResponseDefinitionId == id && item.FormResponseSnapshotJson!.Contains("SavedAnswer"));
			foreach (var item in stored) item.FormResponseSnapshotJson.ShouldBe(savedSnapshots[item.Id]);
			(await _offeringRepository.GetAsync(offering.Id)).InquiryFormDefinitionId.ShouldBe(id);
		});
		var stale = await Should.ThrowAsync<BusinessException>(() => service.RestoreAsync(id, before.ConcurrencyStamp));
		stale.Code.ShouldBe("Admissions.FormConcurrencyConflict");
		var restored = await service.RestoreAsync(id, archived.ConcurrencyStamp);
		restored.Id.ShouldBe(id);
		restored.Status.ShouldBe(AdmissionFormStatus.Published);
		restored.Version.ShouldBe(before.Version);
		JsonSerializer.Serialize(restored.Sections).ShouldBe(JsonSerializer.Serialize(before.Sections));
		(await GetRequiredService<IAdmissionOfferingAppService>().GetAvailabilityAsync(offering.Id)).CanCreateInquiry.ShouldBeTrue();
		// A used offering keeps its setup locked. Future intake uses a separate,
		// unused offering; changing that setup must not migrate the ten saved inquiries.
		await service.ArchiveAsync(id, restored.ConcurrencyStamp);
		var replacement = await service.CloneAsync(id, new() { Name = "Replacement inquiry", Code = $"REPLACEMENT_{Guid.NewGuid():N}"[..24] });
		replacement = await service.PublishAsync(replacement.Id, replacement.ConcurrencyStamp);
		var offerings = GetRequiredService<IAdmissionOfferingAppService>();
		var current = await offerings.GetAsync(offering.Id);
		var update = await EligibilityOfferingInputAsync<UpdateAdmissionOfferingDto>(current);
		update.ConcurrencyStamp = current.ConcurrencyStamp;
		update.InquiryFormDefinitionId = replacement.Id;
		var locked = await Should.ThrowAsync<BusinessException>(() => offerings.UpdateAsync(current.Id, update));
		locked.Code.ShouldBe(AdmissionErrorCodes.OfferingConfigurationLocked);
		var originalCycle = await _cycleRepository.GetAsync(offering.AdmissionCycleId);
		var futureCycle = new AdmissionCycle(Guid.NewGuid(), TestTenantId, "Future form intake",
			$"FUTURE_{Guid.NewGuid():N}"[..24], originalCycle.AcademicYearId, originalCycle.StartDate, originalCycle.EndDate);
		futureCycle.Publish(TestUserId, DateTime.UtcNow);
		await WithUnitOfWorkAsync(() => _cycleRepository.InsertAsync(futureCycle, autoSave: true));
		var futureInput = await EligibilityOfferingInputAsync<CreateAdmissionOfferingDto>(current);
		futureInput.IsActive = false;
		futureInput.InquiryFormDefinitionId = replacement.Id;
		var future = await offerings.CreateAsync(futureCycle.Id, futureInput);
		await offerings.ActivateAsync(future.Id, future.ConcurrencyStamp);
		var capture = await GetRequiredService<IAdmissionInquiryCaptureAppService>().GetContextAsync(future.Id);
		capture.Availability.CanCreateInquiry.ShouldBeTrue();
		capture.Form.ShouldNotBeNull().Id.ShouldBe(replacement.Id);
		var futureOffering = await _offeringRepository.GetAsync(future.Id);
		var input = ManagedInquiryInput(futureOffering);
		input.GuardianResponsibilityConfirmed = true;
		// Cloning upgrades only the new draft to the current core policy. Capture all
		// required parent answers using that definition's actual question codes.
		foreach (var field in replacement.Sections.SelectMany(section => section.Fields))
		{
			var answer = field.BindingKey switch
			{
				"Guardian.FirstName" => input.GuardianFirstName,
				"Guardian.Mobile" => input.GuardianPhone,
				"Guardian.Email" => "meera.archive@example.test",
				"Inquiry.ParentRelationship" => "guardian",
				_ => null
			};
			if (answer != null && input.FormValues.All(value => value.FieldCode != field.Code))
				input.FormValues.Add(new() { FieldCode = field.Code, ValueJson = JsonSerializer.Serialize(answer) });
		}
		var created = await GetRequiredService<IAdmissionInquiryAppService>().CreateAsync(input);
		(await GetRequiredService<IAdmissionInquiryAppService>().GetAsync(created.Id)).FormResponseDefinitionId.ShouldBe(replacement.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var retained = (await inquiries.GetListAsync(item => item.AdmissionOfferingId == offering.Id))
				.Where(item => item.FormResponseDefinitionId == id).ToList();
			retained.Count.ShouldBe(10);
			foreach (var item in retained) item.FormResponseSnapshotJson.ShouldBe(savedSnapshots[item.Id]);
			(await GetRequiredService<IRepository<AdmissionPublicInquiryLink, Guid>>().GetAsync(publicLink.Id))
				.InquiryFormDefinitionId.ShouldBe(id);
		});
	});

	[Fact]
	public Task Form_archive_impact_reports_registration_workflow_stage_and_restore_preserves_it() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionFormAppService>();
		var id = offering.ApplicationFormDefinitionId!.Value;
		var before = await service.GetAsync(id);
		var impact = await service.GetArchiveImpactAsync(id);
		impact.Dependencies.ShouldContain(item => item.Kind == "workflow" && item.Id == offering.WorkflowDefinitionId && item.WorkflowStageId.HasValue);
		var archived = await service.ArchiveAsync(id, impact.ConcurrencyStamp);
		var detail = await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(offering.Id);
		detail.Configuration.Errors.ShouldContain(error => error.FormDefinitionId == id && error.WorkflowStageId.HasValue && error.Message.Contains("archived"));
		var restored = await service.RestoreAsync(id, archived.ConcurrencyStamp);
		restored.Status.ShouldBe(AdmissionFormStatus.Published);
		JsonSerializer.Serialize(restored.Sections).ShouldBe(JsonSerializer.Serialize(before.Sections));
		(await GetRequiredService<IAdmissionOfferingAppService>().GetAsync(offering.Id)).ApplicationFormDefinitionId.ShouldBe(id);
	});

	[Fact]
	public Task Form_lifecycle_rejects_another_tenants_form_and_invalid_restore() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateOptionalIntakeOfferingAsync();
		var service = GetRequiredService<IAdmissionFormAppService>();
		var form = await service.GetAsync(offering.InquiryFormDefinitionId!.Value);
		(await Should.ThrowAsync<BusinessException>(() => service.RestoreAsync(form.Id, form.ConcurrencyStamp)))
			.Code.ShouldBe(AdmissionFormErrorCodes.ArchivedRequired);
		using (_currentTenant.Change(Guid.NewGuid()))
		{
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetArchiveImpactAsync(form.Id));
			await Should.ThrowAsync<EntityNotFoundException>(() => service.RestoreAsync(form.Id, form.ConcurrencyStamp));
		}
	});

	[Fact]
	public Task Form_restore_rejects_legacy_inquiry_definition_without_modifying_it() => RunCertificationScenarioAsync(async () =>
	{
		var id = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var form = new AdmissionFormDefinition(id, TestTenantId, "Legacy inquiry form",
				$"LEGACY_{id:N}"[..24], AdmissionFormLayoutType.SinglePage, purpose: AdmissionFormPurpose.Inquiry);
			form.Publish(TestUserId);
			form.Archive();
			await _formRepository.InsertAsync(form, autoSave: true);
			var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), id, "CONTACT", "Contact details", 0, null);
			await _formSectionRepository.InsertAsync(section, autoSave: true);
			var fields = new (string Code, string Label, AdmissionFormFieldType Type, string Binding, string? Options)[]
			{
				("STUDENT_NAME", "Student name", AdmissionFormFieldType.Text, "Applicant.Name", null),
				("FATHER_NAME", "Father name", AdmissionFormFieldType.Text, "Father.Name", null),
				("FATHER_MOBILE", "Father mobile", AdmissionFormFieldType.Phone, "Father.Mobile", null),
				("MOTHER_NAME", "Mother name", AdmissionFormFieldType.Text, "Mother.Name", null),
				("MOTHER_MOBILE", "Mother mobile", AdmissionFormFieldType.Phone, "Mother.Mobile", null),
				("APPLICANT_GENDER", "Applicant gender", AdmissionFormFieldType.Select, "Applicant.Gender",
					"""[{"value":"Male","label":"Male"},{"value":"Female","label":"Female"},{"value":"OPTION_3","label":"Other"}]""")
			};
			foreach (var (field, index) in fields.Select((field, index) => (field, index)))
			{
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), id, section.Id,
					field.Code, field.Label, field.Type, AdmissionFormBindingType.System, field.Binding, true, false,
					index, null, null, null, null, null, field.Options), autoSave: true);
			}
		});

		var service = GetRequiredService<IAdmissionFormAppService>();
		var before = await service.GetAsync(id);
		var failure = await Should.ThrowAsync<BusinessException>(() => service.RestoreAsync(id, before.ConcurrencyStamp));
		failure.Code.ShouldBe(AdmissionFormErrorCodes.RestoreInvalidDefinition);
		failure.Data["Errors"].ShouldBeOfType<string>().ShouldBe(
			$"{AdmissionInquiryConfiguredFields.MissingSelector}; {AdmissionFormErrorCodes.InvalidOptions}:APPLICANT_GENDER");
		var after = await service.GetAsync(id);
		after.Status.ShouldBe(AdmissionFormStatus.Archived);
		after.ConcurrencyStamp.ShouldBe(before.ConcurrencyStamp);
		JsonSerializer.Serialize(after.Sections).ShouldBe(JsonSerializer.Serialize(before.Sections));
		(await service.GetAuditAsync(id)).ShouldNotContain(entry => entry.Action == "Restored");
	});

	private Task PrepareRestorableInquiryFormAsync(AdmissionOffering offering) => WithUnitOfWorkAsync(async () =>
	{
		var id = offering.InquiryFormDefinitionId!.Value;
		var section = (await _formSectionRepository.GetListAsync(item => item.FormDefinitionId == id)).First();
		var oldMobile = await _formFieldRepository.GetAsync(item => item.FormDefinitionId == id && item.Code == "GUARDIAN_MOBILE");
		await _formFieldRepository.DeleteAsync(oldMobile, autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), id, section.Id,
			"GUARDIAN_MOBILE", "Guardian mobile", AdmissionFormFieldType.Phone, AdmissionFormBindingType.System,
			"Guardian.Mobile", true, false, 3, null, null, null, null, null), autoSave: true);
		await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), id, section.Id,
			"GUARDIAN_FIRST_NAME", "Guardian name", AdmissionFormFieldType.Text, AdmissionFormBindingType.System,
			"Guardian.FirstName", true, false, 5, null, null, null, null, null), autoSave: true);
	});
}
