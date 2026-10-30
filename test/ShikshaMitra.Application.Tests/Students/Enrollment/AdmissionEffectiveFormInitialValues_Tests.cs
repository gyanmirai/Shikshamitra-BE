using System;
using System.Collections.Generic;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionEffectiveFormInitialValues_Tests
{
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
