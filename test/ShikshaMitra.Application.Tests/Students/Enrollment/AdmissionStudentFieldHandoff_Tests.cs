using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Xunit;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.Records;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.BlobStoring;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Uow;
using ShikshaMitra.Common;
using ShikshaMitra.Students.ValueObjects;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Admission_same_address_flags_save_and_reload_distinct_owned_locations() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("SameAddress");
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
			AdmissionFormBindingRegistry.ApplyValue("PermanentAddress.SameAsCurrent", "true", applicant, null);
			AdmissionFormBindingRegistry.ApplyValue("Address.Country", "{\"name\":\"India\",\"isoCode\":\"IN\"}", applicant, null);
			AdmissionFormBindingRegistry.ApplyValue("Address.State", "{\"name\":\"Gujarat\",\"isoCode\":\"GJ\"}", applicant, null);
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var result = await FinalizeAsync(fixture.Application, true, "same-address-owned-location");
		var student = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(result.StudentId));
		var details = student!.CommunicationDetail!;
		details.IsPermanentSameAsLocal.ShouldBeTrue();
		details.IsEmergencySameAsLocal.ShouldBeTrue();
		details.LocalAddress!.Country!.IsoCode.ShouldBe("IN");
		details.PermanentAddress!.Country!.IsoCode.ShouldBe("IN");
		details.EmergencyAddress!.State!.IsoCode.ShouldBe("GJ");
		details.LocalAddress.ShouldNotBeSameAs(details.PermanentAddress);
		details.LocalAddress.Country.ShouldNotBeSameAs(details.PermanentAddress.Country);
	});

	[Fact]
	public Task Admission_sibling_search_is_paged_and_hydrates_selected_scoped_ids() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("SiblingSearch");
		var students = new System.Collections.Generic.List<Student>();
		for (var index = 0; index < 24; index++)
			students.Add(await CreateExistingStudentAsync(fixture.Offering, $"Sibling{index:D2}", "Search"));
		var first = await _formAppService.GetOptionsAsync("StudentSiblings", searchTerm: "Sibling", maxResultCount: 21);
		first.Count.ShouldBe(21);
		var second = await _formAppService.GetOptionsAsync("StudentSiblings", searchTerm: "Sibling", skipCount: 20, maxResultCount: 21);
		second.Count.ShouldBe(4);
		second[0].Value.ShouldBe(students[20].Id.ToString());
		var hydrated = await _formAppService.GetOptionsAsync("StudentSiblings", selectedIds: [students[23].Id, Guid.NewGuid()]);
		hydrated.Count.ShouldBe(1);
		hydrated[0].Value.ShouldBe(students[23].Id.ToString());
		(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionFormDataSourceRegistry>().AreSiblingIdsAllowedAsync([students[23].Id]))).ShouldBeTrue();
		(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionFormDataSourceRegistry>().AreSiblingIdsAllowedAsync([Guid.NewGuid()]))).ShouldBeFalse();
	});

	[Fact]
	public Task Admission_photo_review_rolls_back_draft_and_blob_with_outer_finalization_work() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var photos = GetRequiredService<StudentRecordPhotoAppService>();
		var draftId = Guid.Empty;
		using var transactionalDatabase = await UseRealSerializationTransactionsAsync();
		await Should.ThrowAsync<BusinessException>(() => RunAdmissionTransactionAsync(async () =>
		{
			var draft = await photos.UploadForAdmissionReviewAsync(fixture.Student.Id, new()
			{
				FileName = "photo.png", ContentType = "image/png", Content = AdmissionStudentPhoto.Decode(AdmissionTestPhoto).Content
			});
			draftId = draft.Id;
			throw new BusinessException("Admissions:TestFinalizationFailed");
		}));
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordPhotoDraft, Guid>>().FindAsync(draftId))).ShouldBeNull();
		(await GetRequiredService<IBlobContainer>().ExistsAsync($"student-record-photo/{TestTenantId}/{fixture.Student.Id}/{draftId:N}.png")).ShouldBeFalse();
	});

	[Fact]
	public Task Admission_unchanged_administrative_identifiers_preserve_sync_status() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("IdentityPreserve");
		var student = await CreateExistingStudentAsync(fixture.Offering, "IdentityPreserve", "Student");
		await WithUnitOfWorkAsync(async () =>
		{
			student.ChangeStatus(StudentLifeCycleStatusEnum.Admission);
			await _studentRepository.UpdateAsync(student, true);
			var compliance = new StudentIdentityCompliance(Guid.NewGuid(), TestTenantId, student.Id);
			compliance.SetAdministrativeIdentifiers("PEN-123", "UDISE-123", UdiseSyncStatus.Exported);
			await GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>().InsertAsync(compliance, true);
			await GetRequiredService<StudentIdentityComplianceAppService>().CaptureAdmissionIdentityAsync(student, null, null, "PEN-123", "UDISE-123", null);
		});
		var saved = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>().GetAsync(value => value.StudentId == student.Id));
		saved.UdiseSyncStatus.ShouldBe(UdiseSyncStatus.Exported);
	});

	[Fact]
	public async Task Additional_student_field_saves_and_clears_through_application_form()
	{
		using (_currentTenant.Change(TestTenantId))
		using (_principalAccessor.Change(BuildAdminPrincipal()))
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId,
				userId: TestUserId, description: "Additional student field form test"), autoSave: true);
			var paletteFields = await _formAppService.GetSystemFieldsAsync();
			paletteFields.Count.ShouldBe(107);
			paletteFields.ShouldContain(field => field.Key == "Medical.BloodGroup" &&
				field.FieldType == AdmissionFormFieldType.Select);
			paletteFields.ShouldAllBe(field => !field.Key.StartsWith("FinancialAssistance.",
				StringComparison.OrdinalIgnoreCase));
			var workflow = await CreateAndPublishWorkflowAsync();
			var offering = await CreateAvailableOfferingAsync(workflow.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var section = (await _formSectionRepository.GetListAsync(item =>
					item.FormDefinitionId == offering.ApplicationFormDefinitionId)).Single();
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(
					Guid.NewGuid(), offering.ApplicationFormDefinitionId!.Value, section.Id,
					"BLOOD_GROUP", "Blood group", AdmissionFormFieldType.Select,
					AdmissionFormBindingType.System, "Medical.BloodGroup", false, false, 6,
					null, null, null, null, null,
					AdmissionAdditionalStudentFieldCatalog.Metadata["Medical.BloodGroup"].OptionsJson),
					autoSave: true);
			});
			var applicant = await CreateApplicantAsync("Asha");
			var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);

			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = application.ConcurrencyStamp,
				Values = [new() { FieldCode = "BLOOD_GROUP", ValueJson = "\"O+\"" }]
			});
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id)))
				.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("O+");

			application = await _applicationAppService.GetAsync(application.Id);
			await _applicationAppService.UpdateFormValuesAsync(application.Id, new()
			{
				ConcurrencyStamp = application.ConcurrencyStamp,
				Values = [new() { FieldCode = "BLOOD_GROUP", ValueJson = "\"\"" }]
			});
			(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(applicant.Id)))
				.ExtraProperties.ContainsKey("AdmissionStudentField.Medical.BloodGroup").ShouldBeFalse();
		}
	}

	[Fact]
	public Task Captured_student_fields_survive_finalization_and_student_reload() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Asha");
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
				AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "O+", applicant, null).ShouldBeTrue();
				AdmissionFormBindingRegistry.ApplyValue("PreviousSchool.Name", "Sample School", applicant, null).ShouldBeTrue();
				await _applicantRepository.UpdateAsync(applicant, true);
			});

			var result = await FinalizeAsync(fixture.Application, true, "student-fields-handoff");
			var student = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(result.StudentId));
			student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("O+");
			student.ExtraProperties["AdmissionStudentField.PreviousSchool.Name"].ToString().ShouldBe("Sample School");
			student.MedicalRecord!.BloodGroup.ShouldBe("O+");
			student.PreviousSchoolDetail!.SchoolName.ShouldBe("Sample School");
			var onboarding = await GetRequiredService<IStudentOnboardingAppService>().GetAsync(result.OnboardingCaseId);
			onboarding.CapturedStudentFields.ShouldContain(field =>
				field.Key == "Medical.BloodGroup" && field.Value == "O+");
			onboarding.CapturedStudentFields.ShouldContain(field =>
				field.Key == "PreviousSchool.Name" && field.Value == "Sample School");
		});

	[Fact]
	public Task Captured_student_fields_update_a_reused_student_without_changing_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("Diya", linkExistingStudent: true);
			await WithUnitOfWorkAsync(async () =>
			{
				var applicant = await _applicantRepository.GetAsync(fixture.Application.ApplicantId);
				AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "B+", applicant, null).ShouldBeTrue();
				await _applicantRepository.UpdateAsync(applicant, true);
			});

			var result = await FinalizeAsync(fixture.Application, false, "student-fields-existing-handoff");
			result.ReusedExistingStudent.ShouldBeTrue();
			result.StudentId.ShouldBe(fixture.ExistingStudentId!.Value);
			var student = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(result.StudentId));
			student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ToString().ShouldBe("B+");
			student.MedicalRecord?.BloodGroup.ShouldBeNull();
		});

	private const string AdmissionTestPhoto = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/t0AAAAASUVORK5CYII=";

	[Fact]
	public Task Profile_identity_and_photo_survive_new_student_finalization_and_idempotent_retry() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("IdentityPhoto");
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
			AdmissionFormBindingRegistry.ApplyValue("Applicant.ProfilePhoto", AdmissionTestPhoto, applicant, null);
			AdmissionFormBindingRegistry.ApplyValue("Identity.AadhaarNumber", "234567890123", applicant, null);
			AdmissionFormBindingRegistry.ApplyValue("Identity.PanNumber", "ABCDE1234F", applicant, null);
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
		preview.CapturedStudentFields.Single(field => field.Key == "Applicant.ProfilePhoto").Value.ShouldBe("Photo uploaded");
		var result = await FinalizeAsync(fixture.Application, true, "identity-photo-handoff");
		var student = await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(result.StudentId));
		student!.AdditionalDetail!.PanNo.ShouldBe("ABCDE1234F");
		student.AdditionalDetail.AdharNo.ShouldBeNull();
		student.PhotoBlobName.ShouldNotBeNullOrWhiteSpace();
		var photo = await GetRequiredService<IBlobContainer>().GetAllBytesAsync(student.PhotoBlobName!);
		photo.ShouldBe(AdmissionStudentPhoto.Decode(AdmissionTestPhoto).Content);
		var compliance = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>().GetAsync(value => value.StudentId == student.Id));
		compliance.AadhaarEncrypted.ShouldNotBe("234567890123");
		compliance.AadhaarLast4.ShouldBe("0123");
		compliance.AadhaarVerificationStatus.ShouldBe(IdentityVerificationStatus.Pending);
		(await FinalizeAsync(result.Application, true, "identity-photo-handoff")).StudentId.ShouldBe(result.StudentId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>().CountAsync(value => value.StudentId == student.Id))).ShouldBe(1);
	});

	[Fact]
	public Task Pending_existing_profile_changes_require_explicit_opt_in() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CreateCertificationFixtureAsync("PendingProfile", linkExistingStudent: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await _studentRepository.GetAsync(fixture.ExistingStudentId!.Value);
			student.ChangeStatus(StudentLifeCycleStatusEnum.Admission);
			student.UpdateFamilyInfo(new ParentGuardianDetails("Existing father", null, "Old occupation", null, "9876543210", null,
				null, null, null, null, null,
				new AddressDetails("Office", null, null, "Ahmedabad", new State("Gujarat", "GJ"), new Country("India", "IN"), "380001"),
				new AddressDetails("Home", null, null, "Ahmedabad", new State("Gujarat", "GJ"), new Country("India", "IN"), "380002")), null, null, null);
			student.ApplyVerifiedRegistrationMedical(30, 140, "Retained allergy", "A+", null, null);
			await _studentRepository.UpdateAsync(student, true);
			var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
			AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "AB+", applicant, null);
			AdmissionFormBindingRegistry.ApplyValue("Father.Occupation", "Teacher", applicant, null);
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
		preview.CanApplyProfileFieldsDirectly.ShouldBeTrue();
		var result = await _applicationAppService.FinalizeAsync(fixture.Application.Id, new()
		{
			ConcurrencyStamp = fixture.Application.ConcurrencyStamp, IdempotencyKey = "pending-reviewed-profile",
			ConfirmGuardianContactVerified = true, ApplyStudentProfileFields = true
		});
		var updated = (await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(result.StudentId)))!;
		updated.MedicalRecord!.BloodGroup.ShouldBe("AB+");
		updated.MedicalRecord.Allergies.ShouldBe("Retained allergy");
		updated.MedicalRecord.HeightCm.ShouldBe(140m);
		updated.FamilyDetail!.FatherDetails!.Occupation.ShouldBe("Teacher");
		updated.FamilyDetail.FatherDetails.OfficeAddress!.Address1.ShouldBe("Office");
		updated.FamilyDetail.FatherDetails.LocalAddress!.Country!.IsoCode.ShouldBe("IN");
	});

	[Fact]
	public Task Returning_profile_opt_in_creates_records_requests_and_retry_does_not_duplicate_them() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		var returned = await fixture.Service.ReturnAsync(FreshAdmissionReturn(fixture.Student, fixture.Offering));
		var application = await ReadyAdmissionReturnApplicationAsync(returned.AdmissionApplicationId!.Value);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
			AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "O+", applicant, null);
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		(await _applicationAppService.GetFinalizationPreviewAsync(application.Id)).CanApplyProfileFieldsDirectly.ShouldBeFalse();
		var input = new FinalizeAdmissionApplicationDto { ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = "return-reviewed-profile", ConfirmGuardianContactVerified = true, ApplyStudentProfileFields = true };
		var result = await _applicationAppService.FinalizeAsync(application.Id, input);
		var requests = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordChange, Guid>>().GetListAsync(change => change.StudentId == fixture.Student.Id && change.Section == "medical"));
		requests.Count.ShouldBe(1);
		requests.Single().Status.ShouldBe("Pending");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetWithDetailsAsync(result.StudentId)))!.MedicalRecord?.BloodGroup.ShouldBeNull();
		(await _applicationAppService.FinalizeAsync(application.Id, input)).StudentId.ShouldBe(result.StudentId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(change => change.StudentId == fixture.Student.Id && change.Section == "medical"))).ShouldBe(1);
	});

	[Fact]
	public Task Masked_records_section_rejects_opt_in_without_partial_finalization_or_requests() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await AdmissionReturnFixtureAsync(AdmissionEntryMode.DirectRegistration);
		var returned = await fixture.Service.ReturnAsync(FreshAdmissionReturn(fixture.Student, fixture.Offering));
		var application = await ReadyAdmissionReturnApplicationAsync(returned.AdmissionApplicationId!.Value);
		await WithUnitOfWorkAsync(async () =>
		{
			var applicant = await _applicantRepository.GetAsync(application.ApplicantId);
			AdmissionFormBindingRegistry.ApplyValue("Family.IsSingleChild", "true", applicant, null);
			AdmissionFormBindingRegistry.ApplyValue("Medical.BloodGroup", "O+", applicant, null);
			await _applicantRepository.UpdateAsync(applicant, true);
		});
		var policy = ShikshaMitra.Students.StudentRecords.StudentRecordsPolicy.Starter();
		policy.Access.Masks.Add(new() { FieldGroup = "Medical", AllowedRoleNames = [] });
		await WithUnitOfWorkAsync(async () =>
		{
			var configurations = GetRequiredService<IRepository<ShikshaMitra.Students.StudentRecords.StudentRecordsConfiguration, Guid>>();
			var configuration = await configurations.GetAsync(item => item.TenantId == TestTenantId && item.AcademicYearId == null);
			configuration.SaveDraft(policy.ToJson());
			var version = new ShikshaMitra.Students.StudentRecords.StudentRecordsPolicyVersion(Guid.NewGuid(), TestTenantId,
				configuration.Id, null, configuration.PublishedVersionNumber + 1, policy.ToJson());
			await GetRequiredService<IRepository<ShikshaMitra.Students.StudentRecords.StudentRecordsPolicyVersion, Guid>>().InsertAsync(version, true);
			configuration.Publish(version.Id);
			await configurations.UpdateAsync(configuration, true);
		});
		using var transactionalDatabase = await UseRealSerializationTransactionsAsync();
		(await Should.ThrowAsync<BusinessException>(() => RunAdmissionTransactionAsync(async () => await _applicationAppService.FinalizeAsync(application.Id, new()
		{
			ConcurrencyStamp = application.ConcurrencyStamp, IdempotencyKey = "return-masked-profile", ConfirmGuardianContactVerified = true, ApplyStudentProfileFields = true
		})))).Code.ShouldBe("Students:Records:RestrictedFields");
		(await _applicationAppService.GetAsync(application.Id)).Status.ShouldBe(AdmissionApplicationStatus.OfferAccepted);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(change => change.ClientKey.StartsWith($"admission-{application.Id:N}")))).ShouldBe(0);
	});

	private async Task RunAdmissionTransactionAsync(Func<Task> action)
	{
		var manager = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		using var work = manager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		work.Options.IsTransactional.ShouldBeTrue();
		await action();
		await work.CompleteAsync();
	}
}
