using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Security.UserScope;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(Student Student, Guid DefinitionId, Guid ApplicationId, Guid SubmissionId)> CustomRecordFixtureAsync(bool direct = true, bool repeatable = false, bool canonical = true, bool structuredCondition = false, bool sibling = false, bool standards = false, bool largeFile = false, bool inherited = false)
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "CustomRecord", "Student");
		var applicant = await CreateCertificationApplicantAsync("CustomRecordApplicant", true, true, student.Id);
		var ids = await WithUnitOfWorkAsync(async () =>
		{
			var formId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
			var definition = new AdmissionFormDefinition(formId, TestTenantId, "Captured Registration", $"RECORD-{formId:N}", AdmissionFormLayoutType.SinglePage);
			if (repeatable) AdmissionFormMetadataStore.WriteExperience(definition, new CreateUpdateAdmissionFormDto
			{ Sections = [new() { Code = "CUSTOM", IsRepeatable = true, MinEntries = 1, MaxEntries = 3 }] });
			definition.Publish(TestUserId);
			await GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(definition);
			await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(new(sectionId, formId, "CUSTOM", "School details", 1, null));
			var fieldRepo = GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>();
			async Task Field(string code, AdmissionFormFieldType type, string? rule = null, AdmissionFormBindingType binding = AdmissionFormBindingType.Custom, string? key = null, string? options = null, bool readOnly = false) =>
				await fieldRepo.InsertAsync(new(Guid.NewGuid(), formId, sectionId, code, code, type, binding, key, false, readOnly, 1, null, null, null, null, rule, options));
			await Field("BLOOD", AdmissionFormFieldType.Text);
			await Field("TRANSPORT", AdmissionFormFieldType.Checkbox);
			await Field("PICKUP", AdmissionFormFieldType.Text, "{\"operator\":\"equals\",\"field\":\"TRANSPORT\",\"value\":\"true\"}");
			await Field("ZERO", AdmissionFormFieldType.Number);
			if (largeFile) await Field("UPLOAD", AdmissionFormFieldType.File);
			await Field("ACCESSIBILITY", AdmissionFormFieldType.Textarea, binding: AdmissionFormBindingType.System, key: "Medical.AccessibilitySupport");
			if (canonical) await Field("CANONICAL_BLOOD", AdmissionFormFieldType.Text, binding: AdmissionFormBindingType.System, key: "Medical.BloodGroup");
			await Field("DYNAMIC", AdmissionFormFieldType.Select, options: standards ? "{\"source\":\"StandardsByMediumStage\",\"parentField\":\"STAGE\"}" : "{\"source\":\"EducationBoards\"}");
			if (standards) await Field("STAGE", AdmissionFormFieldType.Text, readOnly: true);
			if (sibling) await Field("SIBLINGS", AdmissionFormFieldType.MultiSelect, options: "{\"source\":\"StudentSiblings\"}");
			if (structuredCondition) await Field("NURSE_NOTE", AdmissionFormFieldType.Text, "{\"operator\":\"equals\",\"field\":\"CANONICAL_BLOOD\",\"value\":\"B+\"}");
			if (repeatable)
			{
				await Field("CHOICES", AdmissionFormFieldType.MultiSelect, options: "[\"one\",\"two\"]");
				await Field("ADDRESS", AdmissionFormFieldType.AddressGroup);
			}
			var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"REC-{Guid.NewGuid():N}", applicant.Id, null,
				offering.AdmissionCycleId, offering.Id, offering.WorkflowDefinitionId!.Value, formId);
			application.Submit(); application.Approve(TestUserId, null);
			var instance = new AdmissionFormInstance(Guid.NewGuid(), TestTenantId, application.Id, formId, 1);
			if (inherited)
			{
				await GetRequiredService<Volo.Abp.Uow.IUnitOfWorkManager>().Current!.SaveChangesAsync();
				var effectiveDefinition = AdmissionApplicationFormService.MapDefinition(definition,
					await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().GetListAsync(x => x.FormDefinitionId == formId),
					await fieldRepo.GetListAsync(x => x.FormDefinitionId == formId));
				effectiveDefinition.Sections[0].Fields.Add(new()
				{
					Id = Guid.NewGuid(), FormDefinitionId = formId, SectionId = sectionId, Code = "INHERITED_NOTE",
					Label = "Learning interests", BindingType = AdmissionFormBindingType.Custom,
					BindingKey = "INHERITED_NOTE", FieldType = AdmissionFormFieldType.Text
				});
				AdmissionEffectiveFormStore.Freeze(instance, new AdmissionFormInheritancePreviewDto
				{
					Definition = effectiveDefinition, InquiryFormDefinitionId = Guid.NewGuid()
				});
			}
			instance.Submit(TestUserId);
			var submittedValues = new Dictionary<string, string> { ["BLOOD"] = "B+", ["TRANSPORT"] = "true", ["PICKUP"] = "Satellite", ["ZERO"] = "0", ["ACCESSIBILITY"] = "Ramp", ["CANONICAL_BLOOD"] = "B+", ["DYNAMIC"] = "" };
			if (inherited) submittedValues["INHERITED_NOTE"] = "Science and drawing";
			if (largeFile) submittedValues["UPLOAD"] = "data:application/pdf;base64," + new string('A', 70000);
			if (sibling) submittedValues["SIBLINGS"] = JsonSerializer.Serialize(new[] { student.Id.ToString() });
			if (standards) submittedValues["STAGE"] = offering.StageId.ToString();
			if (structuredCondition)
			{
				submittedValues["NURSE_NOTE"] = "Original retained note";
				var medicalRepo = GetRequiredService<IRepository<StudentMedicalRecord, Guid>>();
				var medical = await medicalRepo.FindAsync(student.Id) ?? await medicalRepo.InsertAsync(new(student.Id, TestTenantId));
				medical.UpdateMedicalRecords(null, null, null, null, null, null, null, null, null, "B+", null, null); await medicalRepo.UpdateAsync(medical);
			}
			if (repeatable)
			{
				submittedValues = submittedValues.ToDictionary(x => x.Key, x => JsonSerializer.Serialize(new[] { x.Value, x.Key == "TRANSPORT" ? "false" : x.Value }));
				submittedValues["CHOICES"] = JsonSerializer.Serialize(new[] { "[\"one\"]", "[\"two\"]" });
				submittedValues["ADDRESS"] = JsonSerializer.Serialize(new[] { "{\"city\":\"Surat\"}", "{\"city\":\"Ahmedabad\"}" });
			}
			var submitted = new AdmissionFormSubmission(Guid.NewGuid(), TestTenantId, instance.Id, application.Id, 1, TestUserId, JsonSerializer.Serialize(submittedValues));
			var caseId = Guid.NewGuid();
			await _applicationRepository.InsertAsync(application, autoSave: true);
			await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().InsertAsync(instance);
			await GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().InsertAsync(submitted);
			await GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId, instance.Id, "BLOOD", "\"Mutable draft\""));
			var onboarding = new StudentOnboardingCase(caseId, TestTenantId, application.Id, applicant.Id, student.Id, offering.Id,
				student.AcademicYearId!.Value, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, $"custom-{caseId:N}", null, DateTime.UtcNow.AddDays(3));
			await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(onboarding, autoSave: true);
			var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, student.Id, student.AcademicYearId.Value, student.StandardId, student.DivisionId, 7, caseId);
			await GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(enrollment, autoSave: true);
			onboarding.CompleteHistoricalImport(enrollment.Id, student.DivisionId, 7, DateTime.UtcNow);
			await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().UpdateAsync(onboarding, autoSave: true);
			application.FinalizeApprovedAdmission(student.Id, caseId, DateTime.UtcNow);
			await _applicationRepository.UpdateAsync(application, autoSave: true);
			return (FormId: formId, ApplicationId: application.Id, SubmissionId: submitted.Id);
		});
		var policy = StudentRecordsPolicy.Starter();
		if (direct) policy.Changes["Correction"].Mode = "Direct";
		await PublishDirectoryPolicyAsync(policy);
		return (student, ids.FormId, ids.ApplicationId, ids.SubmissionId);
	}

	[Fact]
	public Task Custom_record_details_use_final_submission_not_draft_preserve_typed_false_zero_and_never_write_on_get() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var service = GetRequiredService<IStudentRecordDetailsAppService>();
		var details = await service.GetAsync(fixture.Student.Id);
		var source = details.RegistrationSources.Single();
		source.OriginalValues["BLOOD"].GetString().ShouldBe("B+"); source.CurrentValues["BLOOD"].GetString().ShouldBe("B+");
		source.OriginalValues["ZERO"].GetDecimal().ShouldBe(0); source.OriginalValues["TRANSPORT"].GetBoolean().ShouldBeTrue();
		source.CapturedFieldCodes.ShouldContain("ACCESSIBILITY"); source.CapturedFieldCodes.ShouldNotContain("CANONICAL_BLOOD");
		source.EditableFieldCodes.ShouldContain("BLOOD"); source.Definition!.Version.ShouldBe(1); source.IsBaseline.ShouldBeTrue();
		(await service.GetAsync(fixture.Student.Id)).RegistrationSources.Single().SubmissionId.ShouldBe(fixture.SubmissionId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordCustomProfile, Guid>>().CountAsync())).ShouldBe(0);
	});

	[Fact]
	public Task Custom_record_correction_uses_policy_audit_preserves_original_and_retries_idempotently() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		var input = CustomCorrection(fixture.Student.Id, snapshot, "BLOOD", JsonValue.Create("O+")!);
		var change = await changes.CreateAsync(input); change.Status.ShouldBe("Applied"); change.Action.ShouldBe("Correction");
		change.RequestedByName.ShouldNotBeNullOrWhiteSpace();
		(await changes.CreateAsync(input)).Id.ShouldBe(change.Id);
		var source = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.OriginalValues["BLOOD"].GetString().ShouldBe("B+"); source.CurrentValues["BLOOD"].GetString().ShouldBe("O+");
		var original = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetAsync(fixture.SubmissionId));
		JsonSerializer.Deserialize<Dictionary<string, string>>(original.SnapshotJson)!["BLOOD"].ShouldBe("B+");
		input.ClientKey = Guid.NewGuid().ToString("N");
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(input))).Code.ShouldBe("Students:Records:StaleSnapshot");
	});

	[Fact]
	public Task Custom_record_pending_approval_does_not_replace_current_answers() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(direct: false);
		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var change = await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "BLOOD", JsonValue.Create("O+")!));
		change.Status.ShouldBe("Pending"); change.RequiredApprovals.ShouldBe(1);
		(await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single().CurrentValues["BLOOD"].GetString().ShouldBe("B+");
		var reviewer = Guid.NewGuid(); await EnsureTestIdentityUserAsync(reviewer, $"custom-reviewer-{reviewer:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer), true);
			var roles = GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("admin")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "admin", TestTenantId))).Succeeded.ShouldBeTrue();
			var users = GetRequiredService<IdentityUserManager>(); (await users.AddToRoleAsync((await users.FindByIdAsync(reviewer.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
		});
		using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
			change = await changes.ApproveAsync(change.Id, new() { ConcurrencyStamp = change.ConcurrencyStamp, Reason = "School reviewer confirmed parent evidence" });
		change.Status.ShouldBe("Applied"); change.Decisions.Single().ActorName.ShouldNotBeNullOrWhiteSpace();
		var reviewedSource = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		reviewedSource.CurrentValues["BLOOD"].GetString().ShouldBe("O+"); reviewedSource.OriginalValues["BLOOD"].GetString().ShouldBe("B+");
	});

	[Theory]
	[InlineData("Applicant.Gender", 1, "Female")]
	[InlineData("Applicant.Religion", 1, "Islam")]
	[InlineData("Applicant.MotherTongue", 1, "Hindi")]
	[InlineData("Applicant.SocialCategory", 1, "OBC")]
	public void Custom_record_current_enum_controllers_match_frozen_admission_rule_strings(string binding, int current, string expected) =>
		StudentRecordCurrentRuleReader.AdmissionValue(binding, JsonNode.Parse(current.ToString())).GetString().ShouldBe(expected);

	[Fact]
	public Task Custom_record_dynamic_choices_are_record_bound_and_read_only_lookup_returns_only_selected_labels() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(); var service = GetRequiredService<IStudentRecordDetailsAppService>();
		var source = (await service.GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.OptionsByField["DYNAMIC"].ShouldNotBeEmpty();
		var options = await service.GetFieldOptionsAsync(fixture.Student.Id, new() { SubmissionId = source.SubmissionId, FieldCode = "DYNAMIC" });
		options.ShouldNotBeEmpty(); options.ShouldAllBe(x => x.IsActive && x.Label != x.Value);
		var selected = Guid.Parse(options[0].Value);
		var selectedOnly = await service.GetFieldOptionsAsync(fixture.Student.Id, new() { SubmissionId = source.SubmissionId, FieldCode = "DYNAMIC", SelectedOnly = true, SelectedIds = [selected] });
		selectedOnly.Count.ShouldBe(1); selectedOnly[0].Label.ShouldBe(options[0].Label);
		var unknown = Guid.NewGuid();
		var unavailable = await service.GetFieldOptionsAsync(fixture.Student.Id, new() { SubmissionId = source.SubmissionId, FieldCode = "DYNAMIC", SelectedOnly = true, SelectedIds = [unknown] });
		unavailable.Single().Label.ShouldBe("Option unavailable"); unavailable.Single().IsActive.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => service.GetFieldOptionsAsync(fixture.Student.Id,
			new() { SubmissionId = Guid.NewGuid(), FieldCode = "DYNAMIC" }))).Code.ShouldBe("Students:Records:InvalidCustomSource");
		(await Should.ThrowAsync<BusinessException>(() => service.GetFieldOptionsAsync(fixture.Student.Id,
			new() { SubmissionId = source.SubmissionId, FieldCode = "UNBOUND" }))).Code.ShouldBe("Students:Records:InvalidCustomField");
	});

	[Fact]
	public Task Custom_record_conditional_hide_retains_saved_value_and_false_is_not_erased() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "TRANSPORT", JsonValue.Create(false)!));
		var source = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.CurrentValues["TRANSPORT"].GetBoolean().ShouldBeFalse(); source.CurrentValues["PICKUP"].GetString().ShouldBe("Satellite");
		await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "TRANSPORT", JsonValue.Create(true)!));
		(await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single().CurrentValues["PICKUP"].GetString().ShouldBe("Satellite");
	});

	[Fact]
	public Task Custom_record_public_rule_projection_works_without_compliance_or_general_payload_services() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var source = (await GetRequiredService<StudentRecordRegistrationReader>().GetAsync(fixture.Student.Id)).Single();
			var section = source.Definition!.Sections.Single();
			section.Fields.Add(new() { Id = Guid.NewGuid(), SectionId = section.Id, Code = "GENDER", FieldType = AdmissionFormFieldType.Select,
				BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.Gender" });
			section.Fields.Single(x => x.Code == "PICKUP").VisibilityRuleJson = "{\"operator\":\"equals\",\"field\":\"GENDER\",\"value\":\"Male\"}";
			source.OriginalValues["GENDER"] = JsonSerializer.SerializeToElement("Female"); source.CurrentValues = new(source.OriginalValues);
			using var provider = new ServiceCollection().AddSingleton(GetRequiredService<ICurrentTenant>()).AddSingleton(GetRequiredService<IAsyncQueryableExecuter>()).BuildServiceProvider();
			var reader = new StudentRecordCurrentRuleReader(_studentRepository, Substitute.For<IPermissionChecker>()) { LazyServiceProvider = new AbpLazyServiceProvider(provider) };
			await reader.RefreshAsync(fixture.Student.Id, [source]);
			source.CurrentValues["GENDER"].GetString().ShouldBe("Male"); source.OriginalValues["GENDER"].GetString().ShouldBe("Female");
		});
	});

	[Fact]
	public Task Custom_record_master_dropdown_keeps_more_than_twenty_scoped_choices() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(standards: true);
		var stage = fixture.Student.AcademicMediumStageId!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			for (var index = 0; index < 24; index++)
				await _standardRepository.InsertAsync(await _standardManager.CreateAsync(stage, $"RecordsChoice{index:D2}", 11, null));
		});
		var service = GetRequiredService<IStudentRecordDetailsAppService>(); var source = (await service.GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.OptionsByField["DYNAMIC"].Count.ShouldBeGreaterThan(20);
		(await service.GetFieldOptionsAsync(fixture.Student.Id, new() { SubmissionId = source.SubmissionId, FieldCode = "DYNAMIC", ParentId = stage }))
			.Count.ShouldBeGreaterThan(20);
	});

	[Fact]
	public Task Custom_record_history_hides_values_when_any_recorded_submission_is_no_longer_readable() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var change = await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "BLOOD", JsonValue.Create("O+")!));
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentRecordChange, Guid>>(); var stored = await repository.GetAsync(change.Id);
			var before = JsonNode.Parse(stored.BeforeJson)!; var hidden = before["sources"]![0]!.DeepClone(); hidden["submissionId"] = Guid.NewGuid().ToString();
			before["sources"]!.AsArray().Add(hidden);
			typeof(StudentRecordChange).GetProperty(nameof(StudentRecordChange.BeforeJson))!.SetValue(stored, before.ToJsonString());
			await repository.UpdateAsync(stored, true);
		});
		var history = await changes.GetAsync(change.Id); history.PayloadMasked.ShouldBeTrue(); history.Before.ShouldBeNull(); history.Proposed.ShouldBeNull();
		history.Reason.ShouldBe("Restricted record change"); history.CanApprove.ShouldBeFalse();
	});

	[Fact]
	public Task Custom_record_sibling_labels_and_changes_require_record_eligible_scoped_target() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(sibling: true); var service = GetRequiredService<IStudentRecordDetailsAppService>();
		var source = (await service.GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.OptionsByField["SIBLINGS"].Single(x => x.Value == fixture.Student.Id.ToString()).Label.ShouldContain("CustomRecord");
		var missing = Guid.NewGuid();
		var labels = await service.GetFieldOptionsAsync(fixture.Student.Id, new() { SubmissionId = source.SubmissionId, FieldCode = "SIBLINGS", SelectedOnly = true, SelectedIds = [missing] });
		labels.Single().Label.ShouldBe("Student unavailable"); labels.Single().IsActive.ShouldBeFalse();
		var changes = GetRequiredService<IStudentRecordChangeAppService>(); var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(CustomCorrection(fixture.Student.Id, snapshot, "SIBLINGS", new JsonArray(missing.ToString())))))
			.Code.ShouldBe("Students:Records:InvalidCustomValues");
	});

	[Theory]
	[InlineData("Identity")]
	[InlineData("Contact")]
	[InlineData("Guardian")]
	[InlineData("Medical")]
	[InlineData("Bank")]
	public Task Custom_record_strict_masks_hide_source_values_snapshot_history_and_commands(string group) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		var applied = await changes.CreateAsync(CustomCorrection(fixture.Student.Id, snapshot, "BLOOD", JsonValue.Create("O+")!));
		await WithUnitOfWorkAsync(async () =>
		{
			var configs = await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().GetListAsync();
			var configuration = configs.Single();
			var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks = [new() { FieldGroup = group, AllowedRoleNames = ["other-role"] }];
			configuration.SaveDraft(policy.ToJson());
			var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), TestTenantId, configuration.Id, null, 2, policy.ToJson());
			await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(version);
			configuration.Publish(version.Id); await GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>().UpdateAsync(configuration, true);
		});
		var details = await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id);
		details.CustomDetailsMasked.ShouldBeTrue(); details.CustomDetailsEditable.ShouldBeFalse();
		details.RegistrationSources.Single().Definition.ShouldBeNull(); details.RegistrationSources.Single().OriginalValues.ShouldBeEmpty(); details.RegistrationSources.Single().CurrentValues.ShouldBeEmpty();
		details.RegistrationSources.Single().OptionsByField.ShouldBeEmpty();
		(await Should.ThrowAsync<BusinessException>(() => GetRequiredService<IStudentRecordDetailsAppService>().GetFieldOptionsAsync(fixture.Student.Id,
			new() { SubmissionId = fixture.SubmissionId, FieldCode = "DYNAMIC", SelectedOnly = true }))).Code.ShouldBe("Students:Records:RestrictedFields");
		(await Should.ThrowAsync<BusinessException>(() => changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"))).Code.ShouldBe("Students:Records:RestrictedFields");
		var history = await changes.GetAsync(applied.Id); history.PayloadMasked.ShouldBeTrue(); history.Before.ShouldBeNull(); history.Proposed.ShouldBeNull();
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(CustomCorrection(fixture.Student.Id, snapshot, "BLOOD", JsonValue.Create("A+")!)))).Code.ShouldBe("Students:Records:RestrictedFields");
	});

	[Fact]
	public Task Custom_record_legacy_student_without_form_has_empty_sources_and_empty_default_sections() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "LegacyDetails", "Student"); await GrantCompletedRecordAsync(student);
		var details = await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(student.Id);
		details.RegistrationSources.ShouldBeEmpty(); details.CustomDetailsEditable.ShouldBeFalse();
		details.StructuredSections.Single(x => x.Section == "bank").HasData.ShouldBeFalse();
		details.StructuredSections.Single(x => x.Section == "medical").HasData.ShouldBeFalse();
	});

	[Fact]
	public Task Custom_record_return_reuses_enrollment_shows_differences_without_overwrite_and_review_is_audited() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		await AddCustomReturningSubmissionAsync(fixture.Student, fixture.DefinitionId, fixture.ApplicationId, false);
		var service = GetRequiredService<IStudentRecordDetailsAppService>();
		var sources = (await service.GetAsync(fixture.Student.Id)).RegistrationSources;
		sources.Count.ShouldBe(2); sources[0].IsBaseline.ShouldBeTrue(); sources[1].IsBaseline.ShouldBeFalse();
		sources[1].OriginalValues["BLOOD"].GetString().ShouldBe("A+"); sources[1].CurrentValues["BLOOD"].GetString().ShouldBe("B+");
		sources[1].CurrentValues["NEW_FIELD"].ValueKind.ShouldBe(JsonValueKind.Null);
		sources[1].Differences.ShouldContain(x => x.FieldCode == "BLOOD");
		sources[0].CapturedFieldCodes.ShouldNotContain("NEW_FIELD");
		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!; payload["sources"]![1]!["values"]!["BLOOD"] = "A+";
		var input = new CreateStudentRecordChangeDto { StudentId = fixture.Student.Id, Section = "custom-details", ClientKey = Guid.NewGuid().ToString("N"),
			Reason = "Review returning Registration answer", ExpectedSnapshot = snapshot.Snapshot, Payload = JsonSerializer.SerializeToElement(payload) };
		var applied = await changes.CreateAsync(input); applied.Status.ShouldBe("Applied");
		(await changes.CreateAsync(input)).Id.ShouldBe(applied.Id);
		sources = (await service.GetAsync(fixture.Student.Id)).RegistrationSources;
		sources.All(x => x.CurrentValues["BLOOD"].GetString() == "A+").ShouldBeTrue();
		sources[0].OriginalValues["BLOOD"].GetString().ShouldBe("B+");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(x => x.StudentId == fixture.Student.Id))).ShouldBe(1);
	});

	[Fact]
	public Task Custom_record_return_type_change_is_a_separate_current_field_and_labels_do_not_merge() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync();
		await AddCustomReturningSubmissionAsync(fixture.Student, fixture.DefinitionId, fixture.ApplicationId, true);
		var sources = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources;
		sources[0].CurrentValues["BLOOD"].GetString().ShouldBe("B+");
		sources[1].CurrentValues["BLOOD"].ValueKind.ShouldBe(JsonValueKind.Null); sources[1].OriginalValues["BLOOD"].GetDecimal().ShouldBe(7);
	});

	[Fact]
	public Task Custom_record_repeated_values_are_recursively_typed_and_visibility_is_row_local() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(repeatable: true);
		var source = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.CurrentValues["TRANSPORT"][1].GetBoolean().ShouldBeFalse(); source.CurrentValues["ZERO"][0].GetDecimal().ShouldBe(0);
		source.CurrentValues["CHOICES"][0].ValueKind.ShouldBe(JsonValueKind.Array); source.CurrentValues["ADDRESS"][0].ValueKind.ShouldBe(JsonValueKind.Object);
		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		var pickup = JsonNode.Parse(source.CurrentValues["PICKUP"].GetRawText())!; pickup[0] = "Adajan";
		(await changes.CreateAsync(CustomCorrection(fixture.Student.Id, snapshot, "PICKUP", pickup))).Status.ShouldBe("Applied");
		source = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.CurrentValues["PICKUP"][0].GetString().ShouldBe("Adajan"); source.CurrentValues["PICKUP"][1].GetString().ShouldBe("Satellite");
		var choices = JsonNode.Parse(source.CurrentValues["CHOICES"].GetRawText())!; choices[0] = new JsonArray("two");
		(await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "CHOICES", choices))).Status.ShouldBe("Applied");
		choices[0] = new JsonArray();
		(await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "CHOICES", choices))).Status.ShouldBe("Applied");
		var addresses = JsonNode.Parse(source.CurrentValues["ADDRESS"].GetRawText())!; addresses[0] = new JsonObject();
		(await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "ADDRESS", addresses))).Status.ShouldBe("Applied");
	});

	[Fact]
	public Task Custom_record_repeated_collection_minimum_and_readonly_structural_boundaries_are_enforced() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(repeatable: true, canonical: false); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!;
		foreach (var field in payload["sources"]![0]!["values"]!.AsObject().ToList()) payload["sources"]![0]!["values"]![field.Key] = new JsonArray();
		var input = CustomCorrection(fixture.Student.Id, snapshot, "BLOOD", new JsonArray()); input.Payload = JsonSerializer.SerializeToElement(payload);
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(input))).Code.ShouldBe("Students:Records:InvalidCustomValues");
	});

	[Fact]
	public Task Custom_record_dynamic_choices_validate_tenant_master_data_membership() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var input = CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "DYNAMIC", JsonValue.Create(Guid.NewGuid().ToString())!);
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(input))).Code.ShouldBe("Students:Records:InvalidCustomValues");
	});

	[Fact]
	public Task Custom_record_repeated_canonical_members_block_structural_shifts() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(repeatable: true); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"); var payload = JsonNode.Parse(snapshot.Data.GetRawText())!;
		foreach (var field in payload["sources"]![0]!["values"]!.AsObject()) field.Value!.AsArray().Add(field.Value[0]!.DeepClone());
		var input = CustomCorrection(fixture.Student.Id, snapshot, "BLOOD", new JsonArray()); input.Payload = JsonSerializer.SerializeToElement(payload);
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(input))).Code.ShouldBe("Students:Records:OwnedFieldChange");
	});

	[Fact]
	public Task Custom_record_structured_condition_uses_current_medical_value_and_retains_original_note() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true); var details = GetRequiredService<IStudentRecordDetailsAppService>();
		(await details.GetAsync(fixture.Student.Id)).RegistrationSources.Single().CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		await WithUnitOfWorkAsync(async () =>
		{
			var medicalRepo = GetRequiredService<IRepository<StudentMedicalRecord, Guid>>(); var medical = await medicalRepo.GetAsync(fixture.Student.Id);
			medical.UpdateMedicalRecords(null, null, null, null, null, null, null, null, null, "O+", null, null); await medicalRepo.UpdateAsync(medical, true);
		});
		var source = (await details.GetAsync(fixture.Student.Id)).RegistrationSources.Single();
		source.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+"); source.OriginalValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		source.CurrentValues["NURSE_NOTE"].GetString().ShouldBe("Original retained note");
		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var hiddenSnapshot = await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details");
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(CustomCorrection(fixture.Student.Id,
			hiddenSnapshot, "NURSE_NOTE", JsonValue.Create("Hidden edit")!)))).Code.ShouldBe("Students:Records:HiddenCustomFieldChange");
	});

	[Fact]
	public Task Custom_record_write_preserves_current_values_and_provenance_outside_the_visible_source_set() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(); var changes = GetRequiredService<IStudentRecordChangeAppService>();
		await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "BLOOD", JsonValue.Create("O+")!));
		var hiddenId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<StudentRecordCustomProfile, Guid>>(); var profile = (await repository.GetListAsync()).Single();
			var values = JsonNode.Parse(profile.ValuesJson)!; values["hidden-source:UNIQUE:Text:False"] = "Retained current value";
			var provenance = JsonSerializer.Serialize(new[] { new { SubmissionId = hiddenId } });
			profile.UpdateCurrent(StudentRecordCustomDetailsService.MergeProvenance(profile.SourcesJson, provenance),
				StudentRecordCustomDetailsService.MergeProvenance(profile.SchemaJson, provenance), values.ToJsonString());
			await repository.UpdateAsync(profile, true);
		});
		await changes.CreateAsync(CustomCorrection(fixture.Student.Id, await changes.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "BLOOD", JsonValue.Create("A+")!));
		var retained = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<StudentRecordCustomProfile, Guid>>().GetListAsync()).Single());
		JsonNode.Parse(retained.ValuesJson)!["hidden-source:UNIQUE:Text:False"]!.ToString().ShouldBe("Retained current value");
		retained.SourcesJson.ShouldContain(hiddenId.ToString()); retained.SchemaJson.ShouldContain(hiddenId.ToString());
	});

	private async Task AddCustomReturningSubmissionAsync(Student student, Guid originalDefinitionId, Guid originalApplicationId, bool typeChange)
	{
		var returningApplicant = await CreateCertificationApplicantAsync("CustomReturningApplicant", true, true, student.Id);
		await WithUnitOfWorkAsync(async () =>
		{
		var originalApplication = await _applicationRepository.GetAsync(originalApplicationId);
		var offering = await _offeringRepository.GetAsync(originalApplication.AdmissionOfferingId);
		var formId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
		var original = await GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().GetAsync(originalDefinitionId);
		var definition = new AdmissionFormDefinition(formId, TestTenantId, "Renamed school form", original.Code, AdmissionFormLayoutType.SinglePage, 2, originalDefinitionId);
		definition.Publish(TestUserId); await GetRequiredService<IRepository<AdmissionFormDefinition, Guid>>().InsertAsync(definition);
		await GetRequiredService<IRepository<AdmissionFormSectionDefinition, Guid>>().InsertAsync(new(sectionId, formId, "CUSTOM", "Renamed section", 1, null));
		var fieldRepo = GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>();
		foreach (var field in await fieldRepo.GetListAsync(x => x.FormDefinitionId == originalDefinitionId))
			await fieldRepo.InsertAsync(new(Guid.NewGuid(), formId, sectionId, field.Code, "New label " + field.Label,
				typeChange && field.Code == "BLOOD" ? AdmissionFormFieldType.Number : field.FieldType,
				field.BindingType, field.BindingKey, false, false, field.DisplayOrder, null, null, null, null, field.VisibilityRuleJson));
		await fieldRepo.InsertAsync(new(Guid.NewGuid(), formId, sectionId, "NEW_FIELD", "Second language", AdmissionFormFieldType.Text,
			AdmissionFormBindingType.Custom, null, false, false, 3, null, null, null, null, null));
		var application = new AdmissionApplication(Guid.NewGuid(), TestTenantId, $"RETURN-{Guid.NewGuid():N}", returningApplicant.Id, null,
			offering.AdmissionCycleId, offering.Id, originalApplication.WorkflowDefinitionId, formId);
		application.Submit(); application.Approve(TestUserId, null);
		var instance = new AdmissionFormInstance(Guid.NewGuid(), TestTenantId, application.Id, formId, 2); instance.Submit(TestUserId);
		var submitted = new AdmissionFormSubmission(Guid.NewGuid(), TestTenantId, instance.Id, application.Id, 1, TestUserId,
			JsonSerializer.Serialize(new Dictionary<string, string> { ["BLOOD"] = typeChange ? "7" : "A+", ["TRANSPORT"] = "false", ["PICKUP"] = "New area",
				["ZERO"] = "0", ["ACCESSIBILITY"] = "Lift", ["CANONICAL_BLOOD"] = "A+", ["NEW_FIELD"] = "Hindi" }));
		var caseId = Guid.NewGuid();
		await _applicationRepository.InsertAsync(application, autoSave: true); await GetRequiredService<IRepository<AdmissionFormInstance, Guid>>().InsertAsync(instance);
		await GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().InsertAsync(submitted);
		var enrollment = (await GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetListAsync(x => x.StudentId == student.Id)).Single();
		var onboarding = new StudentOnboardingCase(caseId, TestTenantId, application.Id, application.ApplicantId, student.Id, offering.Id,
			student.AcademicYearId!.Value, offering.BoardId, offering.MediumId, offering.StageId, offering.StandardId, $"return-{caseId:N}", null, DateTime.UtcNow.AddDays(3));
		onboarding.CompleteHistoricalImport(enrollment.Id, student.DivisionId, 7, DateTime.UtcNow);
		await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().InsertAsync(onboarding, autoSave: true);
		application.FinalizeApprovedAdmission(student.Id, caseId, DateTime.UtcNow); await _applicationRepository.UpdateAsync(application, autoSave: true);
		});
	}

	private static CreateStudentRecordChangeDto CustomCorrection(Guid studentId, StudentRecordSectionSnapshotDto snapshot, string field, JsonNode value)
	{
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!;
		payload["sources"]![0]!["values"]![field] = value.DeepClone();
		return new() { StudentId = studentId, Section = "custom-details", Reason = "Parent confirmed corrected school details", ClientKey = Guid.NewGuid().ToString("N"),
			ExpectedSnapshot = snapshot.Snapshot, Payload = JsonSerializer.SerializeToElement(payload) };
	}
}
