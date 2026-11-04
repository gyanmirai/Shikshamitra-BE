using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("captured")]
	[InlineData("alternate")]
	[InlineData("posted-alternate")]
	[InlineData("generic")]
	[InlineData("posted-generic")]
	public Task Sibling_registration_keeps_distinct_guardian_profession_separate_from_confirmed_father(string identity) => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var section = await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(Guid.NewGuid(), formId, "PARENT", "Parent", 0, null), true);
			foreach (var (code, binding) in new[] { ("FATHER_JOB", "Father.Occupation"), ("GUARDIAN_JOB", "Guardian.Occupation"),
				("CONTACT", "Guardian.FirstName"), ("ALTERNATE", "Guardian.AlternateName") })
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, code, code,
					AdmissionFormFieldType.Text, AdmissionFormBindingType.System, binding, false, false, 0, null, null, null, null, null), true);
		});
		var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId: formId);
		var source = await CreateApplicantAsync("Father profile source");
		await WithUnitOfWorkAsync(async () =>
		{
			var saved = await _applicantRepository.GetAsync(source.Id);
			AdmissionSiblingParentSelection.CaptureParent(saved, "father", "Amit", "9876543210", null);
			saved.SetProperty("AdmissionStudentField.Father.Occupation", "Teacher");
			saved.SetProperty("AdmissionStudentField.Mother.Occupation", "Doctor");
			await _applicantRepository.UpdateAsync(saved, true);
		});
		await StartFixtureApplicationAsync(source.Id, offering.Id);
		var target = await WithUnitOfWorkAsync(async () =>
		{
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId, "Distinct guardian target", null, null, null, null, null, Guid.NewGuid());
			AdmissionSiblingParentSelection.Confirm(applicant, new() { SourceApplicantId = source.Id, Relationship = "father", Name = "Amit", Mobile = "9876543210" }, TestUserId, DateTime.UtcNow, "9876543210", null);
			if (identity == "captured") AdmissionFamilyContactCapture.Capture(applicant, new() { MainContactRelationship = "father",
				OtherGuardian = new() { FullName = "Alice", Relationship = "Aunt", Mobile = "9876543212" } }, "father");
			if (identity == "alternate") applicant.SetProperty("AdmissionStudentField.Guardian.AlternateName", "Alice");
			applicant.SetProperty("AdmissionStudentField.Mother.Occupation", "Target mother job");
			return await _applicantRepository.InsertAsync(applicant, true);
		});
		var application = await StartFixtureApplicationAsync(target.Id, offering.Id);
		if (identity == "generic") await WithUnitOfWorkAsync(async () =>
		{
			var form = (await _applicationAppService.GetFormAsync(application.Id));
			var values = GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>();
			var contact = await values.SingleAsync(value => value.FormInstanceId == form.FormInstanceId && value.FieldCode == "CONTACT");
			contact.SetValue("\"Robert\"");
			await values.UpdateAsync(contact, true);
			var occupation = await values.SingleAsync(value => value.FormInstanceId == form.FormInstanceId && value.FieldCode == "GUARDIAN_JOB");
			occupation.SetValue("\"\"");
			await values.UpdateAsync(occupation, true);
		});
		var projected = await _applicationAppService.GetFormAsync(application.Id);
		if (!identity.StartsWith("posted-", StringComparison.Ordinal))
		{
			projected.AutoFilledFieldCodes.ShouldNotContain("GUARDIAN_JOB");
			projected.Values.ShouldNotContain(value => value.FieldCode == "GUARDIAN_JOB" && AdmissionApplicationValueConverter.Unwrap(value.ValueJson) == "Teacher");
			AdmissionApplicationValueConverter.Unwrap(projected.Values.Single(value => value.FieldCode == "FATHER_JOB").ValueJson).ShouldBe("Teacher");
		}
		var input = new UpdateAdmissionApplicationFormValuesDto { ConcurrencyStamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp,
			Values = [new() { FieldCode = "GUARDIAN_JOB", ValueJson = "\"Nurse\"" }, new() { FieldCode = "FATHER_JOB", ValueJson = "\"Teacher\"" }],
			AutoFilledFieldCodes = ["FATHER_JOB"] };
		if (identity == "captured") input.AutoFilledFieldCodes.Add("guardian_job"); // A request hint cannot assign this Guardian to Father.
		if (identity == "posted-alternate") input.Values.Add(new() { FieldCode = "ALTERNATE", ValueJson = "\"Alice\"" });
		if (identity == "posted-generic") input.Values.Add(new() { FieldCode = "CONTACT", ValueJson = "\"Robert\"" });
		await _applicationAppService.UpdateFormValuesAsync(application.Id, input);
		var savedTarget = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.Id));
		AdmissionStudentProfileMapping.Read(savedTarget, "Guardian.Occupation").ShouldBe("Nurse");
		AdmissionStudentProfileMapping.Read(savedTarget, "Father.Occupation").ShouldBe("Teacher");
		AdmissionStudentProfileMapping.Read(savedTarget, "Mother.Occupation").ShouldBe("Target mother job");
		var copied = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormChangeHistory, Guid>>()
			.GetListAsync(history => history.FormInstanceId == projected.FormInstanceId && history.ChangeType == "FamilyPrefilled"));
		copied.Where(history => JsonSerializer.Deserialize<JsonElement>(history.ChangedFieldsJson!).GetProperty("ClientReported").GetBoolean())
			.ShouldNotContain(history => (history.ChangedFieldsJson ?? string.Empty).Contains("guardian_job", StringComparison.OrdinalIgnoreCase));
		var savedSource = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.Id));
		AdmissionStudentProfileMapping.Read(savedSource, "Father.Occupation").ShouldBe("Teacher");
		AdmissionStudentProfileMapping.Read(savedSource, "Mother.Occupation").ShouldBe("Doctor");
	});

	[Fact]
	public Task Sibling_prefill_ordinary_form_paths_do_not_add_standalone_applicant_reads() => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId: formId);
		var applicant = await CreateApplicantAsync("Ordinary no sibling");
		var application = await StartFixtureApplicationAsync(applicant.Id, offering.Id);
		async Task<(int Reads, int ApplicantReads, string[] Queries)> Capture(Func<Task> action) => await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var counter = new AcademicQueryCounter(db.Database.GetDbConnection());
			await action();
			var queries = counter.ReadCommands.ToArray();
			return (counter.ReadCount, queries.Count(query => System.Text.RegularExpressions.Regex.Match(query, "\\bFROM\\s+\"([^\"]+)\"",
				System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value == "AppAdmissionApplicants"), queries);
		});
		var get = await Capture(async () => { await _applicationAppService.GetFormAsync(application.Id); });
		var stamp = (await _applicationAppService.GetAsync(application.Id)).ConcurrencyStamp;
		var save = await Capture(async () => { await _applicationAppService.UpdateFormValuesAsync(application.Id, new() { ConcurrencyStamp = stamp, Values = [] }); });
		(await _applicationAppService.SubmitAsync(application.Id)).ValidationErrors.ShouldBeEmpty();
		var submittedGet = await Capture(async () => { await _applicationAppService.GetFormAsync(application.Id); });
		var capturePath = System.IO.Path.GetFullPath(".artifacts/sibling-family-autofill/task1-fix1-query-last.json");
		System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(capturePath)!);
		await System.IO.File.WriteAllTextAsync(capturePath, JsonSerializer.Serialize(new
		{
			Get = new { get.Reads, get.ApplicantReads, get.Queries }, Save = new { save.Reads, save.ApplicantReads, save.Queries },
			SubmittedGet = new { submittedGet.Reads, submittedGet.ApplicantReads, submittedGet.Queries }
		}, new JsonSerializerOptions { WriteIndented = true }));
		get.ApplicantReads.ShouldBe(0); save.ApplicantReads.ShouldBe(0); submittedGet.ApplicantReads.ShouldBe(0);
	});

	[Theory]
	[InlineData(false, false, true)]
	[InlineData(false, true, true)]
	[InlineData(true, false, true)]
	[InlineData(true, true, true)]
	[InlineData(false, false, false)]
	[InlineData(false, true, false)]
	[InlineData(true, false, false)]
	[InlineData(true, true, false)]
	public Task Sibling_registration_keeps_authored_occupation_when_source_changes_after_GET(bool editedGeneric, bool reverseOrder, bool includeHints) => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var section = await _formSectionRepository.InsertAsync(new AdmissionFormSectionDefinition(Guid.NewGuid(), formId, "PARENT", "Parent", 0, null), true);
			foreach (var (code, binding) in new[] { ("CANONICAL", "Father.Occupation"), ("GENERIC", "Guardian.Occupation") })
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id, code, code,
					AdmissionFormFieldType.Text, AdmissionFormBindingType.System, binding, false, false, 0, null, null, null, null, null), true);
		});
		var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId: formId);
		var source = await CreateApplicantAsync("Source before change");
		await WithUnitOfWorkAsync(async () => { var saved = await _applicantRepository.GetAsync(source.Id);
			AdmissionSiblingParentSelection.CaptureParent(saved, "father", "Shared Father", "9876543210", null); await _applicantRepository.UpdateAsync(saved, true); });
		var sourceApplication = await StartFixtureApplicationAsync(source.Id, offering.Id);
		var target = await WithUnitOfWorkAsync(async () =>
		{
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId, "Target after change", null, null, null, null, null, Guid.NewGuid());
			AdmissionSiblingParentSelection.Confirm(applicant, new() { SourceApplicantId = source.Id, Relationship = "father", Name = "Shared Father", Mobile = "9876543210" }, TestUserId, DateTime.UtcNow, "9876543210", null);
			return await _applicantRepository.InsertAsync(applicant, true);
		});
		var targetApplication = await StartFixtureApplicationAsync(target.Id, offering.Id);
		async Task SaveSource(string occupation) => _ = await _applicationAppService.UpdateFormValuesAsync(sourceApplication.Id, new()
			{ ConcurrencyStamp = (await _applicationAppService.GetAsync(sourceApplication.Id)).ConcurrencyStamp,
				Values = [new() { FieldCode = "CANONICAL", ValueJson = JsonSerializer.Serialize(occupation) }] });
		await SaveSource("Engineer");
		var projected = await _applicationAppService.GetFormAsync(targetApplication.Id);
		projected.AutoFilledFieldCodes.OrderBy(code => code).ShouldBe(["CANONICAL", "GENERIC"]);
		await SaveSource("Architect");
		var editedCode = editedGeneric ? "GENERIC" : "CANONICAL";
		var automaticCode = editedGeneric ? "CANONICAL" : "GENERIC";
		var input = new UpdateAdmissionApplicationFormValuesDto { ConcurrencyStamp = (await _applicationAppService.GetAsync(targetApplication.Id)).ConcurrencyStamp,
			Values = [new() { FieldCode = editedCode, ValueJson = "\"Teacher\"" }, new() { FieldCode = automaticCode, ValueJson = "\"Engineer\"" }] };
		if (reverseOrder) input.Values.Reverse();
		if (includeHints) input.AutoFilledFieldCodes = [automaticCode];
		else
		{
			var failure = await Should.ThrowAsync<Volo.Abp.BusinessException>(() => _applicationAppService.UpdateFormValuesAsync(targetApplication.Id, input));
			failure.Code.ShouldBe(AdmissionApplicationErrorCodes.ValidationFailed);
			AdmissionStudentProfileMapping.Read(await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.Id)), "Father.Occupation").ShouldBeNullOrEmpty();
			return;
		}
		await _applicationAppService.UpdateFormValuesAsync(targetApplication.Id, input);
		var savedTarget = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.Id));
		AdmissionStudentProfileMapping.Read(savedTarget, "Father.Occupation").ShouldBe("Teacher");
		var reloaded = await _applicationAppService.GetFormAsync(targetApplication.Id);
		AdmissionApplicationValueConverter.Unwrap(reloaded.Values.Single(value => value.FieldCode == editedCode).ValueJson).ShouldBe("Teacher");
		reloaded.Values.Where(value => value.FieldCode is "CANONICAL" or "GENERIC").ShouldNotContain(value =>
			!string.IsNullOrWhiteSpace(AdmissionApplicationValueConverter.Unwrap(value.ValueJson)) && AdmissionApplicationValueConverter.Unwrap(value.ValueJson) != "Teacher");
	});

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public Task Sibling_registration_automatically_reuses_saved_parent_profile_without_GET_writes(bool sourceSavedBeforeTargetStarts) => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var section = new AdmissionFormSectionDefinition(Guid.NewGuid(), formId, "PARENTS", "Parents", 0, null);
			await _formSectionRepository.InsertAsync(section, true);
			foreach (var binding in new[] { "Father.Name", "Father.Occupation", "Father.Qualification", "Father.Designation", "Father.Employer", "Mother.Name", "Mother.Occupation", "Guardian.FirstName", "Guardian.Occupation", "Applicant.PlaceOfBirth", "Address.City" })
				await _formFieldRepository.InsertAsync(new AdmissionFormFieldDefinition(Guid.NewGuid(), formId, section.Id,
					binding.Replace('.', '_'), binding, AdmissionFormFieldType.Text, AdmissionFormBindingType.System, binding,
					false, false, 0, null, null, null, null, null), true);
		});
		var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId: formId);
		var source = await CreateApplicantAsync("Older");
		var sourceApplication = await StartFixtureApplicationAsync(source.Id, offering.Id);
		async Task SaveSource() => _ = await _applicationAppService.UpdateFormValuesAsync(sourceApplication.Id, new()
		{
			ConcurrencyStamp = (await _applicationAppService.GetAsync(sourceApplication.Id)).ConcurrencyStamp,
			Values = new[] { ("FATHER_NAME", "Shared Father"), ("FATHER_OCCUPATION", "Engineer"), ("FATHER_QUALIFICATION", "BTech"),
				("FATHER_DESIGNATION", "Manager"), ("FATHER_EMPLOYER", "School"), ("MOTHER_NAME", "Shared Mother"), ("MOTHER_OCCUPATION", "Teacher"),
				("APPLICANT_PLACEOFBIRTH", "Source city") }
				.Select(item => new AdmissionFormValueDto { FieldCode = item.Item1, ValueJson = JsonSerializer.Serialize(item.Item2) }).ToList()
		});
		if (sourceSavedBeforeTargetStarts) await SaveSource();
		var target = await WithUnitOfWorkAsync(async () =>
		{
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId, "Younger", null, "Patel",
				new DateTime(2018, 1, 1), "Male", null, Guid.NewGuid());
			AdmissionSiblingParentSelection.Confirm(applicant, new() { SourceApplicantId = source.Id, Relationship = "father",
				Name = "Shared Father", Mobile = "9876543210" }, TestUserId, DateTime.UtcNow, "9876543210", null);
			return await _applicantRepository.InsertAsync(applicant, true);
		});
		var targetApplication = await StartFixtureApplicationAsync(target.Id, offering.Id);
		if (!sourceSavedBeforeTargetStarts) await SaveSource();
		var values = GetRequiredService<IRepository<AdmissionFormFieldValue, Guid>>();
		var histories = GetRequiredService<IRepository<AdmissionFormChangeHistory, Guid>>();
		var users = GetRequiredService<IRepository<Volo.Abp.Identity.IdentityUser, Guid>>();
		var accountCount = await WithUnitOfWorkAsync(() => users.CountAsync());
		var historyCount = await WithUnitOfWorkAsync(() => histories.CountAsync());
		var before = await WithUnitOfWorkAsync(() => values.GetListAsync());
		var form = await _applicationAppService.GetFormAsync(targetApplication.Id);
		form.FamilyPrefill.ShouldNotBeNull();
		form.FamilyPrefill.SourceApplicantId.ShouldBe(source.Id);
		form.FamilyPrefill.Relationship.ShouldBe("father");
		form.AutoFilledFieldCodes.Contains("FATHER_OCCUPATION").ShouldBe(!sourceSavedBeforeTargetStarts);
		AdmissionApplicationValueConverter.Unwrap(form.Values.Single(value => value.FieldCode == "FATHER_OCCUPATION").ValueJson).ShouldBe("Engineer");
		AdmissionApplicationValueConverter.Unwrap(form.Values.Single(value => value.FieldCode == "FATHER_QUALIFICATION").ValueJson).ShouldBe("BTech");
		form.Values.ShouldNotContain(value => value.FieldCode == "APPLICANT_PLACEOFBIRTH" && AdmissionApplicationValueConverter.Unwrap(value.ValueJson) == "Source city");
		var after = await WithUnitOfWorkAsync(() => values.GetListAsync());
		after.OrderBy(value => value.Id).Select(value => (value.Id, value.ValueJson, value.LastModifiedAt))
			.ShouldBe(before.OrderBy(value => value.Id).Select(value => (value.Id, value.ValueJson, value.LastModifiedAt)));
		(await WithUnitOfWorkAsync(() => histories.CountAsync())).ShouldBe(historyCount);
		var firstSaveValues = form.Values.Where(value => value.FieldCode is "FATHER_OCCUPATION" or "FATHER_QUALIFICATION" ||
			!sourceSavedBeforeTargetStarts && value.FieldCode == "GUARDIAN_OCCUPATION").Select(value => new AdmissionFormValueDto
			{ FieldCode = value.FieldCode, ValueJson = !sourceSavedBeforeTargetStarts && value.FieldCode == "FATHER_OCCUPATION" ? "\"\"" : value.ValueJson }).ToList();
		await _applicationAppService.UpdateFormValuesAsync(targetApplication.Id, new()
		{
			ConcurrencyStamp = (await _applicationAppService.GetAsync(targetApplication.Id)).ConcurrencyStamp,
			Values = firstSaveValues,
			AutoFilledFieldCodes = firstSaveValues.Where(value => form.AutoFilledFieldCodes.Contains(value.FieldCode) &&
				value.ValueJson == form.Values.Single(item => item.FieldCode == value.FieldCode).ValueJson).Select(value => value.FieldCode).ToList()
		});
		var reloaded = await _applicationAppService.GetFormAsync(targetApplication.Id);
		AdmissionApplicationValueConverter.Unwrap(reloaded.Values.Single(value => value.FieldCode == "FATHER_OCCUPATION").ValueJson).ShouldBe(sourceSavedBeforeTargetStarts ? "Engineer" : string.Empty);
		var savedTarget = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(target.Id));
		(AdmissionStudentProfileMapping.Read(savedTarget, "Father.Occupation") ?? string.Empty).ShouldBe(sourceSavedBeforeTargetStarts ? "Engineer" : string.Empty);
		AdmissionStudentProfileMapping.Read(savedTarget, "Guardian.Occupation").ShouldBeNull();
		var audit = await _applicationAppService.GetFormHistoryAsync(targetApplication.Id);
		audit.ShouldContain(item => item.ChangeType == "FamilyPrefilled");
		await _applicationAppService.UpdateFormValuesAsync(targetApplication.Id, new()
		{
			ConcurrencyStamp = (await _applicationAppService.GetAsync(targetApplication.Id)).ConcurrencyStamp,
			Values = [new() { FieldCode = "FATHER_OCCUPATION", ValueJson = "\"\"" }, new() { FieldCode = "FATHER_QUALIFICATION", ValueJson = "\"Own qualification\"" }]
		});
		reloaded = await _applicationAppService.GetFormAsync(targetApplication.Id);
		AdmissionApplicationValueConverter.Unwrap(reloaded.Values.Single(value => value.FieldCode == "FATHER_OCCUPATION").ValueJson).ShouldBeEmpty();
		AdmissionApplicationValueConverter.Unwrap(reloaded.Values.Single(value => value.FieldCode == "FATHER_QUALIFICATION").ValueJson).ShouldBe("Own qualification");
		reloaded.AutoFilledFieldCodes.ShouldNotContain("FATHER_OCCUPATION");
		var unchangedSource = await WithUnitOfWorkAsync(() => _applicantRepository.GetAsync(source.Id));
		AdmissionStudentProfileMapping.Read(unchangedSource, "Father.Occupation").ShouldBe("Engineer");
		AdmissionStudentProfileMapping.Read(unchangedSource, "Father.Qualification").ShouldBe("BTech");
		(await _applicationAppService.SubmitAsync(targetApplication.Id)).ValidationErrors.ShouldBeEmpty();
		var submitted = await _applicationAppService.GetFormAsync(targetApplication.Id);
		submitted.Status.ShouldBe(AdmissionFormInstanceStatus.Submitted); submitted.AutoFilledFieldCodes.ShouldBeEmpty();
		(await WithUnitOfWorkAsync(() => users.CountAsync())).ShouldBe(accountCount);
	});

	[Theory]
	[InlineData("unconfirmed")]
	[InlineData("self")]
	[InlineData("other-family")]
	[InlineData("missing")]
	[InlineData("denied")]
	[InlineData("foreign-tenant")]
	public Task Sibling_registration_rejects_invalid_or_inaccessible_source_without_metadata(string invalidSource) => RunCertificationScenarioAsync(async () =>
	{
		var target = await CreateApplicantAsync("Target");
		var source = await WithUnitOfWorkAsync(async () =>
		{
			var applicant = new AdmissionApplicant(Guid.NewGuid(), invalidSource == "foreign-tenant" ? Guid.NewGuid() : TestTenantId,
				invalidSource == "other-family" ? (await CreateApplicantAsync("Other family")).FamilyId : target.FamilyId,
				"Source", null, null, null, null, null, Guid.NewGuid());
			applicant.SetProperty("AdmissionStudentField.Father.Occupation", "Private job");
			return await _applicantRepository.InsertAsync(applicant, true);
		});
		if (invalidSource != "unconfirmed") AdmissionSiblingParentSelection.Confirm(target, new()
		{
			SourceApplicantId = invalidSource == "self" ? target.Id : invalidSource == "missing" ? Guid.NewGuid() : source.Id,
			Relationship = "father", Name = "Shared Father", Mobile = "9876543210"
		}, TestUserId, DateTime.UtcNow, "9876543210", null);
		var definition = new AdmissionFormDto { Sections = [new() { Code = "PARENT", Fields = [new()
			{ Code = "JOB", BindingKey = "Father.Occupation", BindingType = AdmissionFormBindingType.System }] }] };
		var instance = new AdmissionFormInstance(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), Guid.NewGuid(), 1);
		var result = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionSiblingRegistrationPrefillService>().ProjectAsync(target, instance, definition, []));
		result.Values.ShouldBeEmpty(); result.AutoFilledFieldCodes.ShouldBeEmpty(); result.FamilyPrefill.ShouldBeNull();
	});

	[Fact]
	public Task Sibling_registration_reads_current_scoped_student_parent_without_stale_missing_field_fallback() => RunCertificationScenarioAsync(async () =>
	{
		var workflow = await CreateAndPublishCurrentWorkflowAsync();
		var formId = AdmissionFormStageAccessService.ReadFormDefinitionId(workflow.Stages.Single(stage => stage.IsEntryStage).ConfigurationJson)!.Value;
		var offering = await CreateAvailableOfferingAsync(workflow.Id, registrationFormId: formId);
		var source = await CreateApplicantAsync("Source");
		await StartFixtureApplicationAsync(source.Id, offering.Id);
		await WithUnitOfWorkAsync(async () =>
		{
			var student = await CreateExistingStudentAsync(offering, "Source", "Patel");
			student.UpdateFamilyInfo(new ShikshaMitra.Students.ValueObjects.ParentGuardianDetails("Shared Father", null, "Current job", null,
				"9876543210", null, null, null, "Current title", "Current employer", null, null, null), null, null, null);
			await _studentRepository.UpdateAsync(student, true);
			var savedSource = await _applicantRepository.GetAsync(source.Id);
			savedSource.LinkStudent(student.Id);
			savedSource.SetProperty("AdmissionStudentField.Father.Occupation", "Old job");
			savedSource.SetProperty("AdmissionStudentField.Father.Qualification", "Old qualification");
			await _applicantRepository.UpdateAsync(savedSource, true);
		});
		var target = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId, "Target", null, null, null, null, null, Guid.NewGuid());
		AdmissionSiblingParentSelection.Confirm(target, new() { SourceApplicantId = source.Id, Relationship = "father", Name = "Shared Father", Mobile = "9876543210" },
			TestUserId, DateTime.UtcNow, "9876543210", null);
		var instance = new AdmissionFormInstance(Guid.NewGuid(), TestTenantId, Guid.NewGuid(), formId, 1);
		var definition = new AdmissionFormDto { Sections = [new() { Code = "PARENTS", Fields = new[] { "Occupation", "Qualification", "Designation", "Employer" }
			.Select(key => new AdmissionFormFieldDto { Code = key.ToUpperInvariant(), BindingKey = "Father." + key, BindingType = AdmissionFormBindingType.System }).ToList() }] };
		definition.Id = formId;
		definition.Sections[0].Id = Guid.NewGuid(); definition.Sections[0].Name = "Parents";
		foreach (var field in definition.Sections[0].Fields)
		{
			field.Id = Guid.NewGuid(); field.FormDefinitionId = formId; field.SectionId = definition.Sections[0].Id; field.Label = field.Code;
		}
		var result = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionSiblingRegistrationPrefillService>().ProjectAsync(target, instance, definition, [], initial: true));
		AdmissionApplicationValueConverter.Unwrap(result.Values.Single(value => value.FieldCode == "OCCUPATION").ValueJson).ShouldBe("Current job");
		AdmissionApplicationValueConverter.Unwrap(result.Values.Single(value => value.FieldCode == "EMPLOYER").ValueJson).ShouldBe("Current employer");
		result.Values.ShouldNotContain(value => value.FieldCode == "QUALIFICATION");
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View).Returns(false);
		var denied = ActivatorUtilities.CreateInstance<AdmissionSiblingRegistrationPrefillService>(GetRequiredService<IServiceProvider>(), permissions);
		denied.LazyServiceProvider = new AbpLazyServiceProvider(GetRequiredService<IServiceProvider>());
		var deniedResult = await WithUnitOfWorkAsync(() => denied.ProjectAsync(target, instance, definition, [], initial: true));
		deniedResult.FamilyPrefill.ShouldBeNull(); deniedResult.Values.ShouldBeEmpty();
		var inquiryOnly = await WithUnitOfWorkAsync(async () =>
		{
			var applicant = new AdmissionApplicant(Guid.NewGuid(), TestTenantId, source.FamilyId, "Pending sibling", null, null, null, null, null, Guid.NewGuid());
			applicant.SetProperty("AdmissionStudentField.Father.Occupation", "Private inquiry job");
			await _applicantRepository.InsertAsync(applicant, true);
			var inquiry = new AdmissionInquiry(Guid.NewGuid(), TestTenantId, "PREFILL-" + Guid.NewGuid().ToString("N"), offering.AdmissionCycleId, offering.Id,
				"Pending sibling", "9876543210", null, AdmissionInquirySource.Manual, "Prefill permission fixture");
			inquiry.LinkIdentity(source.FamilyId, applicant.Id);
			await GetRequiredService<IRepository<AdmissionInquiry, Guid>>().InsertAsync(inquiry, true);
			return applicant;
		});
		AdmissionSiblingParentSelection.Confirm(target, new() { SourceApplicantId = inquiryOnly.Id, Relationship = "father", Name = "Shared Father", Mobile = "9876543210" },
			TestUserId, DateTime.UtcNow, "9876543210", null);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.View).Returns(true);
		var applicationViewOnly = await WithUnitOfWorkAsync(() => denied.ProjectAsync(target, instance, definition, [], initial: true));
		applicationViewOnly.FamilyPrefill.ShouldBeNull(); applicationViewOnly.Values.ShouldBeEmpty();
		var unscopedActor = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(unscopedActor, "prefill-unscoped-" + unscopedActor.ToString("N"));
		using (_principalAccessor.Change(ProfilePrincipal(unscopedActor)))
		{
			var inaccessible = await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionSiblingRegistrationPrefillService>().ProjectAsync(target, instance, definition, [], initial: true));
			inaccessible.FamilyPrefill.ShouldBeNull(); inaccessible.Values.ShouldBeEmpty();
		}
		instance.Submit(TestUserId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionSiblingRegistrationPrefillService>().ProjectAsync(target, instance, definition, []))).AutoFilledFieldCodes.ShouldBeEmpty();
		instance.Reopen();
		(await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionSiblingRegistrationPrefillService>().ProjectAsync(target, instance, definition, []))).AutoFilledFieldCodes.ShouldBeEmpty();
	});
}
