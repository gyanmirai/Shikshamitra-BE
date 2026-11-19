using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryFormPrefill_Tests
{
	[Fact]
	public void Reused_field_code_must_not_copy_inquiry_notes_into_applying_class()
	{
		var notes = Field("FIELD_10", "Inquiry notes", AdmissionFormFieldType.Textarea);
		var applyingClass = Field("FIELD_10", "Applying class", AdmissionFormFieldType.Text);

		AdmissionInquiryFormPrefill.ResolveCustomValue(applyingClass, [notes],
			new Dictionary<string, string> { ["FIELD_10"] = "\"Please call after school\"" })
			.ShouldBeNull();
	}

	[Fact]
	public void Matching_class_field_copies_its_value_when_target_choice_accepts_it()
	{
		var inquiryClass = Field("FIELD_5", "Applying class", AdmissionFormFieldType.Text);
		var registrationClass = Field("FIELD_5", "Applying class", AdmissionFormFieldType.Select,
			optionsJson: "[{\"value\":\"Class 1\",\"label\":\"Class 1\"}]");

		AdmissionInquiryFormPrefill.ResolveCustomValue(registrationClass, [inquiryClass],
			new Dictionary<string, string> { ["FIELD_5"] = "\"Class 1\"" })
			.ShouldBe("\"Class 1\"");
	}

	[Fact]
	public void Target_choice_rejects_a_value_outside_its_options()
	{
		var inquiryClass = Field("FIELD_5", "Applying class", AdmissionFormFieldType.Text);
		var registrationClass = Field("FIELD_5", "Applying class", AdmissionFormFieldType.Select,
			optionsJson: "[{\"value\":\"Class 1\",\"label\":\"Class 1\"}]");

		AdmissionInquiryFormPrefill.ResolveCustomValue(registrationClass, [inquiryClass],
			new Dictionary<string, string> { ["FIELD_5"] = "\"Class 10\"" })
			.ShouldBeNull();
	}

	[Fact]
	public void Target_choice_accepts_a_string_option()
	{
		var inquiryClass = Field("FIELD_5", "Applying class", AdmissionFormFieldType.Text);
		var registrationClass = Field("FIELD_5", "Applying class", AdmissionFormFieldType.Select,
			optionsJson: "[\"Class 1\",\"Class 2\"]");

		AdmissionInquiryFormPrefill.ResolveCustomValue(registrationClass, [inquiryClass],
			new Dictionary<string, string> { ["FIELD_5"] = "\"Class 1\"" })
			.ShouldBe("\"Class 1\"");
	}

	[Fact]
	public void Explicit_shared_binding_can_transfer_between_differently_named_fields()
	{
		var source = Field("INQUIRY_CLASS", "Class interested in", AdmissionFormFieldType.Text,
			bindingKey: "ADMISSION_CLASS");
		var target = Field("REGISTRATION_CLASS", "Applying class", AdmissionFormFieldType.Text,
			bindingKey: "ADMISSION_CLASS");

		AdmissionInquiryFormPrefill.ResolveCustomValue(target, [source],
			new Dictionary<string, string> { ["ADMISSION_CLASS"] = "\"Class 1\"" })
			.ShouldBe("\"Class 1\"");
	}

	private static AdmissionFormFieldDefinition Field(string code, string label,
		AdmissionFormFieldType type, string? bindingKey = null, string? optionsJson = null) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), code, label, type,
			AdmissionFormBindingType.Custom, bindingKey ?? code, true, false, 0,
			null, null, null, null, null, optionsJson);
}
