using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private static SaveStudentRecordFormDto InlineFormEdit(StudentRecordSectionSnapshotDto snapshot, params (string Code, JsonNode? Value)[] values)
	{
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!;
		foreach (var value in values) payload["sources"]![0]!["values"]![value.Code] = value.Value;
		return new() { ClientKey = Guid.NewGuid().ToString("N"), Reason = "Correct retained Registration form details", ExpectedSnapshot = snapshot.Snapshot, Payload = JsonSerializer.SerializeToElement(payload) };
	}

	[Fact]
	public Task Inline_form_save_applies_mapped_and_custom_answers_atomically_without_changing_original_or_generic_approval() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(direct: false, structuredCondition: true);
		var original = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetAsync(fixture.SubmissionId));
		var originalJson = original.SnapshotJson;
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var details = await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id);
		details.RegistrationSources.Single().EditableFieldCodes.ShouldNotContain("CANONICAL_BLOOD");
		details.FormSources.Single().FormEditableFieldCodes.ShouldContain("CANONICAL_BLOOD");
		var anotherYear = await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id, Guid.NewGuid());
		anotherYear.RegistrationSources.ShouldBeEmpty(); anotherYear.FormSources.Single().SubmissionId.ShouldBe(fixture.SubmissionId);
		var snapshot = await service.GetSnapshotAsync(fixture.Student.Id);
		var input = InlineFormEdit(snapshot, ("BLOOD", JsonValue.Create("O+")), ("CANONICAL_BLOOD", JsonValue.Create("O+")));
		var result = await service.SaveAsync(fixture.Student.Id, input);
		result.Status.ShouldBe("Applied"); result.RequiredApprovals.ShouldBe(0); result.Section.ShouldBe("registration-form");
		result.RequestedByName.ShouldNotBeNullOrWhiteSpace(); result.AppliedAt.ShouldNotBeNull(); result.Reason.ShouldBe(input.Reason);
		result.Before!.Value.GetProperty("sources")[0].GetProperty("values").GetProperty("CANONICAL_BLOOD").GetString().ShouldBe("B+");
		result.Proposed!.Value.GetProperty("sources")[0].GetProperty("values").GetProperty("CANONICAL_BLOOD").GetString().ShouldBe("O+");
		(await service.SaveAsync(fixture.Student.Id, input)).Id.ShouldBe(result.Id);
		var updated = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		updated.CurrentValues["BLOOD"].GetString().ShouldBe("O+"); updated.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+");
		updated.OriginalValues["BLOOD"].GetString().ShouldBe("B+"); updated.OriginalValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetAsync(fixture.SubmissionId))).SnapshotJson.ShouldBe(originalJson);
		input.ClientKey = Guid.NewGuid().ToString("N");
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Student.Id, input))).Code.ShouldBe("Students:Records:StaleSnapshot");
		var generic = GetRequiredService<IStudentRecordChangeAppService>();
		(await generic.CreateAsync(CustomCorrection(fixture.Student.Id, await generic.GetSnapshotAsync(fixture.Student.Id, "custom-details"), "BLOOD", JsonValue.Create("A+")!))).Status.ShouldBe("Pending");
	});

	[Fact]
	public Task Inline_form_invalid_mixed_save_rolls_back_and_never_creates_partial_audit() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true);
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var input = InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("CANONICAL_BLOOD", JsonValue.Create("O+")), ("ZERO", new JsonObject { ["invalid"] = true }));
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Student.Id, input))).Code.ShouldBe("Students:Records:InvalidCustomValues");
		(await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single().CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(x => x.StudentId == fixture.Student.Id && x.Section == "registration-form"))).ShouldBe(0);
	});

	[Fact]
	public Task Inline_form_mapped_permission_remains_available_when_unknown_custom_group_is_masked() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true);
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks = [new() { FieldGroup = "Bank", AllowedRoleNames = ["other-role"] }];
		await SetInlineFormPolicyAsync(policy, 2);
		var details = await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id);
		details.CustomDetailsMasked.ShouldBeTrue(); details.RegistrationSources.Single().Definition.ShouldBeNull();
		var source = details.FormSources.Single(); source.FormMaskedFieldCodes.ShouldContain("BLOOD");
		source.CurrentValues["BLOOD"].GetString().ShouldBe("Restricted"); source.OriginalValues["BLOOD"].GetString().ShouldBe("Restricted");
		source.FormEditableFieldCodes.ShouldContain("CANONICAL_BLOOD"); source.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("B+");
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var snapshot = await service.GetSnapshotAsync(fixture.Student.Id);
		(await service.SaveAsync(fixture.Student.Id, InlineFormEdit(snapshot, ("CANONICAL_BLOOD", JsonValue.Create("O+"))))).Status.ShouldBe("Applied");
		var restricted = InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("BLOOD", JsonValue.Create("A+")));
		(await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Student.Id, restricted))).Code.ShouldBe("Students:Records:OwnedFieldChange");
		await SetInlineFormPolicyAsync(StudentRecordsPolicy.Starter(), 3);
		var unmasked = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		unmasked.CurrentValues["BLOOD"].GetString().ShouldBe("B+"); unmasked.CurrentValues["CANONICAL_BLOOD"].GetString().ShouldBe("O+");
	});
	private Task SetInlineFormPolicyAsync(StudentRecordsPolicy policy, int number) => WithUnitOfWorkAsync(async () =>
	{
		var configurations = GetRequiredService<IRepository<StudentRecordsConfiguration, Guid>>();
		var configuration = (await configurations.GetListAsync()).Single(); configuration.SaveDraft(policy.ToJson());
		var version = new StudentRecordsPolicyVersion(Guid.NewGuid(), TestTenantId, configuration.Id, null, number, policy.ToJson());
		await GetRequiredService<IRepository<StudentRecordsPolicyVersion, Guid>>().InsertAsync(version);
		configuration.Publish(version.Id); await configurations.UpdateAsync(configuration, autoSave: true);
	});

	[Fact]
	public Task Inline_form_repeatable_custom_answers_preserve_hidden_values_and_owned_row_structure() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(repeatable: true, canonical: false);
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var snapshot = await service.GetSnapshotAsync(fixture.Student.Id);
		(await service.SaveAsync(fixture.Student.Id, InlineFormEdit(snapshot, ("TRANSPORT", new JsonArray(false, false))))).Status.ShouldBe("Applied");
		var current = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		current.CurrentValues["PICKUP"][0].GetString().ShouldBe("Satellite"); current.CurrentValues["TRANSPORT"][0].GetBoolean().ShouldBeFalse();
	});

	[Fact]
	public Task Inline_form_general_edit_preserves_unrepresented_nullable_details_identity_and_placement() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(structuredCondition: true);
		await WithUnitOfWorkAsync(async () =>
		{
			var source = (await GetRequiredService<StudentRecordRegistrationReader>().GetAsync(fixture.Student.Id)).Single();
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), fixture.DefinitionId,
				source.Definition!.Sections.Single().Id, "FIRST", "First name", AdmissionFormFieldType.Text, AdmissionFormBindingType.System,
				"Applicant.FirstName", true, false, 10, null, null, null, null, null, null));
		});
		var before = await WithUnitOfWorkAsync(async () =>
		{
			var student = (await _studentRepository.GetRegistrationAggregateAsync(fixture.Student.Id))!;
			return new { student.StandardId, student.DivisionId, student.GrNo, student.AdditionalDetail!.CastCategory, student.AdditionalDetail.DateOfBirth,
				student.AdditionalDetail.Gender, student.AdditionalDetail.AdharNo, student.AdditionalDetail.HaveDisability, student.AdditionalDetail.IsBPLStudent };
		});
		var service = GetRequiredService<IStudentRecordFormAppService>();
		(await service.SaveAsync(fixture.Student.Id, InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("FIRST", JsonValue.Create("Riya"))))).Status.ShouldBe("Applied");
		await WithUnitOfWorkAsync(async () =>
		{
			var student = (await _studentRepository.GetRegistrationAggregateAsync(fixture.Student.Id))!;
			student.FirstName.ShouldBe("Riya"); student.StandardId.ShouldBe(before.StandardId); student.DivisionId.ShouldBe(before.DivisionId); student.GrNo.ShouldBe(before.GrNo);
			student.AdditionalDetail!.CastCategory.ShouldBe(before.CastCategory); student.AdditionalDetail.DateOfBirth.ShouldBe(before.DateOfBirth);
			student.AdditionalDetail.Gender.ShouldBe(before.Gender); student.AdditionalDetail.AdharNo.ShouldBe(before.AdharNo);
			student.AdditionalDetail.HaveDisability.ShouldBe(before.HaveDisability); student.AdditionalDetail.IsBPLStudent.ShouldBe(before.IsBPLStudent);
		});
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Inline_form_newly_required_always_visible_field_blocks_controller_change(bool repeatable) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(repeatable: repeatable, canonical: false);
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var falseValue = repeatable ? (JsonNode)new JsonArray(false, false) : JsonValue.Create(false)!;
		await service.SaveAsync(fixture.Student.Id, InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("TRANSPORT", falseValue)));
		await WithUnitOfWorkAsync(async () =>
		{
			var source = (await GetRequiredService<StudentRecordRegistrationReader>().GetAsync(fixture.Student.Id)).Single();
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), fixture.DefinitionId,
				source.Definition!.Sections.Single().Id, "REQUIRED_NOTE", "Required note", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom,
				null, false, false, 20, null, null, null, null, null, null, "{\"operator\":\"equals\",\"field\":\"TRANSPORT\",\"value\":\"true\"}"));
		});
		var trueValue = repeatable ? (JsonNode)new JsonArray(true, false) : JsonValue.Create(true)!;
		var invalid = InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("TRANSPORT", trueValue));
		var error = await Should.ThrowAsync<BusinessException>(() => service.SaveAsync(fixture.Student.Id, invalid));
		error.Code.ShouldBe("Students:Records:InvalidCustomValues");
		error.Data["FieldErrors"].ShouldBeOfType<Dictionary<string, string>>().ShouldContainKey(repeatable ? "REQUIRED_NOTE[0]" : "REQUIRED_NOTE");
		var current = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single().CurrentValues["TRANSPORT"];
		(repeatable ? current[0] : current).GetBoolean().ShouldBeFalse();
	});

	[Fact]
	public Task Inline_form_binary_answers_remain_readonly_summaries_without_blocking_other_edits() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(largeFile: true);
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var source = (await GetRequiredService<IStudentRecordDetailsAppService>().GetAsync(fixture.Student.Id)).FormSources.Single();
		source.CurrentValues["UPLOAD"].GetString().ShouldBe("Attachment recorded"); source.FormEditableFieldCodes.ShouldNotContain("UPLOAD");
		var snapshot = await service.GetSnapshotAsync(fixture.Student.Id);
		snapshot.Data.GetRawText().Length.ShouldBeLessThan(65536);
		(await service.SaveAsync(fixture.Student.Id, InlineFormEdit(snapshot, ("BLOOD", JsonValue.Create("O+"))))).Status.ShouldBe("Applied");
		var original = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<AdmissionFormSubmission, Guid>>().GetAsync(fixture.SubmissionId));
		original.SnapshotJson.ShouldContain(new string('A', 70000));
	});

	[Theory]
	[InlineData(AdmissionFormFieldType.MultiSelect)]
	[InlineData(AdmissionFormFieldType.AddressGroup)]
	public Task Inline_form_empty_required_structured_answer_is_rejected(AdmissionFormFieldType type) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await CustomRecordFixtureAsync(canonical: false);
		await WithUnitOfWorkAsync(async () =>
		{
			var source = (await GetRequiredService<StudentRecordRegistrationReader>().GetAsync(fixture.Student.Id)).Single();
			await GetRequiredService<IRepository<AdmissionFormFieldDefinition, Guid>>().InsertAsync(new(Guid.NewGuid(), fixture.DefinitionId,
				source.Definition!.Sections.Single().Id, "REQUIRED_STRUCTURED", "Required structured answer", type, AdmissionFormBindingType.Custom,
				null, true, false, 21, null, null, null, null, null, null));
		});
		var service = GetRequiredService<IStudentRecordFormAppService>();
		var empty = type == AdmissionFormFieldType.MultiSelect ? (JsonNode)new JsonArray() : new JsonObject();
		var error = await Should.ThrowAsync<BusinessException>(async () => await service.SaveAsync(fixture.Student.Id,
			InlineFormEdit(await service.GetSnapshotAsync(fixture.Student.Id), ("REQUIRED_STRUCTURED", empty))));
		error.Code.ShouldBe("Students:Records:InvalidCustomValues");
		error.Data["FieldErrors"].ShouldBeOfType<Dictionary<string, string>>().ShouldContainKey("REQUIRED_STRUCTURED");
	});
}
