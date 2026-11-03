using System;
using System.Collections.Generic;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEffectiveFormInitialValues_Tests
{
	[Fact]
	public void Structured_first_name_does_not_prefill_with_the_whole_original_inquiry_name()
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			"Aarav Original Inquiry Name", null, null, null, null, null, Guid.NewGuid());
		var field = new AdmissionFormFieldDto { BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.FirstName" };
		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, applicant, null,
			new Dictionary<string, string> { ["Applicant.Name"] = "\"Aarav Original Inquiry Name\"" }).ShouldBe("null");
	}

	[Fact]
	public void Existing_student_structured_name_remains_authoritative_over_a_matching_full_name_snapshot()
	{
		var applicant = Applicant(existingStudent: true);
		var field = new AdmissionFormFieldDto { BindingType = AdmissionFormBindingType.System, BindingKey = "Applicant.FirstName" };
		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, applicant, null,
			new Dictionary<string, string> { ["Applicant.Name"] = "\"Aarav\"" }).ShouldBe("\"Aarav\"");
	}

	[Fact]
	public void Readonly_custom_question_retains_its_captured_answer_instead_of_reverting_to_default()
	{
		var field = Field(AdmissionFormBindingType.Custom, "SCHOOL_QUESTION", readOnly: true);
		field.DefaultValue = "\"Original default\"";

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string> { ["SOURCE_QUESTION"] = "\"Captured answer\"" })
			.ShouldBe("\"Captured answer\"");
	}

	[Fact]
	public void Explicit_source_clear_does_not_revert_to_the_default_or_current_applicant_answer()
	{
		var field = Field(AdmissionFormBindingType.System, "Applicant.DateOfBirth");
		field.DefaultValue = "\"2019-05-14\"";

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string> { ["SOURCE_QUESTION"] = "null" }).ShouldBe("null");
	}

	[Fact]
	public void Readonly_system_question_remains_server_authoritative()
	{
		var field = Field(AdmissionFormBindingType.System, "Applicant.DateOfBirth", readOnly: true);
		var actual = AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string> { ["SOURCE_QUESTION"] = "\"1990-01-01\"" });

		JsonSerializer.Deserialize<DateTime>(actual!).Date.ShouldBe(new DateTime(2018, 1, 2));
	}

	[Theory]
	[InlineData(AdmissionFormBindingType.System, "Applicant.Name")]
	[InlineData(AdmissionFormBindingType.Custom, "STUDENT_NAME")]
	public void Inquiry_full_name_reference_keeps_the_captured_name_without_rebuilding_it_from_registration_names(AdmissionFormBindingType type, string binding)
	{
		var field = Field(type, binding, readOnly: true);
		field.FieldIdentity = "system:APPLICANT.NAME";
		field.InheritedFrom!.SourceBindingKey = binding;
		field.InheritedFrom.SourceReadOnly = false;

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string> { [binding] = "\"Aarav Original Inquiry Name\"" })
			.ShouldBe("\"Aarav Original Inquiry Name\"");
	}

	[Theory]
	[InlineData(AdmissionFormBindingType.System, "Applicant.Name")]
	[InlineData(AdmissionFormBindingType.Custom, "STUDENT_NAME")]
	public void Missing_inquiry_full_name_reference_does_not_invent_a_value_from_registration_names(AdmissionFormBindingType type, string binding)
	{
		var field = Field(type, binding, readOnly: true);
		field.FieldIdentity = "system:APPLICANT.NAME";
		field.InheritedFrom!.SourceBindingKey = binding;
		field.InheritedFrom.SourceReadOnly = false;

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string>()).ShouldBeNull();
	}

	[Fact]
	public void Originally_readonly_inquiry_name_keeps_existing_server_authority()
	{
		var field = Field(AdmissionFormBindingType.System, "Applicant.Name", readOnly: true);
		field.InheritedFrom!.SourceBindingKey = "Applicant.Name";
		field.InheritedFrom.SourceReadOnly = true;

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string> { ["Applicant.Name"] = "\"Untrusted override\"" })
			.ShouldBe("\"Aarav Shah\"");
	}

	[Fact]
	public void Matched_legacy_custom_question_prefills_from_its_original_key_after_adopting_a_system_binding()
	{
		var field = Field(AdmissionFormBindingType.System, "Applicant.DateOfBirth");
		field.InheritedFrom!.SourceBindingKey = "DATE_OF_BIRTH";

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant(), null,
			new Dictionary<string, string> { ["DATE_OF_BIRTH"] = "\"2019-05-14\"" })
			.ShouldBe("\"2019-05-14\"");
	}

	[Fact]
	public void Existing_student_contact_prefill_keeps_the_current_verified_guardian()
	{
		var applicant = Applicant(existingStudent: true);
		var guardian = new AdmissionGuardian(Guid.NewGuid(), applicant.TenantId, applicant.FamilyId,
			"Verified parent", null, "9876543210", "verified@example.test", AdmissionGuardianRelationship.Parent, true);
		var field = Field(AdmissionFormBindingType.System, "Guardian.FirstName");

		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, applicant, guardian,
			new Dictionary<string, string> { ["SOURCE_QUESTION"] = "\"Other parent\"" })
			.ShouldBe("\"Verified parent\"");
	}

	private static AdmissionFormFieldDto Field(AdmissionFormBindingType type, string binding, bool readOnly = false) => new()
	{
		BindingType = type, BindingKey = binding, ReadOnly = readOnly,
		InheritedFrom = new() { InquiryFormDefinitionId = Guid.NewGuid(), SourceFieldCode = "SOURCE", SourceBindingKey = "SOURCE_QUESTION" }
	};

	private static AdmissionApplicant Applicant(bool existingStudent = false) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"Aarav", null, "Shah", new DateTime(2018, 1, 2), null, existingStudent ? Guid.NewGuid() : null, Guid.NewGuid());
}
