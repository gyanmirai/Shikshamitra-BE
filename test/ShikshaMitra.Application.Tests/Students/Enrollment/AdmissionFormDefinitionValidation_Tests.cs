using System;
using System.Collections.Generic;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormDefinitionValidation_Tests
{
	[Theory]
	[InlineData("Applicant.FirstName")]
	[InlineData("Applicant.MiddleName")]
	[InlineData("Applicant.LastName")]
	public void Registration_rejects_writable_full_name_mixed_with_any_structured_name_part(string part)
	{
		AdmissionFormFieldDefinition Field(string key) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), key.Replace('.', '_').ToUpperInvariant(), key,
			AdmissionFormFieldType.Text, AdmissionFormBindingType.System, key, true, false, 0, null, null, null, null, null);
		var result = new AdmissionFormValidationResultDto();
		AdmissionFormAppService.ValidateApplicantNameMappings(AdmissionFormPurpose.Registration, [Field("Applicant.Name"), Field(part)], result);
		result.Errors.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData(true, AdmissionFormBindingType.System)]
	[InlineData(false, AdmissionFormBindingType.Custom)]
	public void Registration_keeps_readonly_full_name_context_and_custom_name_surveys_publishable(bool fullNameReadOnly, AdmissionFormBindingType bindingType)
	{
		var fields = new[]
		{
			new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "FULL_NAME", "Name context", AdmissionFormFieldType.Text, bindingType, "Applicant.Name", false, fullNameReadOnly, 0, null, null, null, null, null),
			new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LAST_NAME", "Last name", AdmissionFormFieldType.Text, AdmissionFormBindingType.System, "Applicant.LastName", false, false, 1, null, null, null, null, null)
		};
		var result = new AdmissionFormValidationResultDto();
		AdmissionFormAppService.ValidateApplicantNameMappings(AdmissionFormPurpose.Registration, fields, result);
		result.Errors.ShouldBeEmpty();
	}
	[Theory]
	[InlineData("Applicant.Gender", "[{\"value\":\"Male\",\"label\":\"Male\"},{\"value\":\"Female\",\"label\":\"Female\"}]", false)]
	[InlineData("Applicant.Gender", "[{\"value\":\"1\",\"label\":\"Male\"}]", true)]
	[InlineData("Inquiry.ParentRelationship", "[{\"value\":\"father\"},{\"value\":\"mother\"},{\"value\":\"guardian\"}]", false)]
	[InlineData("Inquiry.ParentRelationship", "[{\"value\":\"father\"},{\"value\":\"mother\"},{\"value\":\"uncle\"}]", true)]
	public void System_choices_are_usable_by_their_runtime_contract(string binding, string options, bool invalid)
	{
		var field = new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CHOICE", "Choice",
			AdmissionFormFieldType.Select, AdmissionFormBindingType.System, binding, true, false, 0, null, null, null, null, null, options);
		var result = new AdmissionFormValidationResultDto();
		AdmissionFormAppService.ValidateOptions(field, new HashSet<string>(StringComparer.OrdinalIgnoreCase), result);
		if (invalid) result.Errors.ShouldContain("Admissions.FormInvalidOptions:CHOICE");
		else result.Errors.ShouldBeEmpty();
	}
	[Theory]
	[InlineData("email")]
	[InlineData("EMAIL")]
	public void Email_format_used_by_admission_form_templates_is_valid(string format)
	{
		var result = new AdmissionFormValidationResultDto();

		AdmissionFormAppService.ValidateValidationJson(
			$"{{\"format\":\"{format}\"}}",
			"GUARDIAN_EMAIL",
			new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			result);

		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Unknown_format_remains_invalid()
	{
		var result = new AdmissionFormValidationResultDto();

		AdmissionFormAppService.ValidateValidationJson(
			"{\"format\":\"unsupported\"}",
			"CUSTOM_FIELD",
			new HashSet<string>(StringComparer.OrdinalIgnoreCase),
			result);

		result.Errors.ShouldContain("Admissions.FormInvalidValidationRule:CUSTOM_FIELD");
	}

	[Theory]
	[InlineData("[{\"value\":\"OPTION_1\",\"label\":\"\"}]", true)]
	[InlineData("[{\"value\":\"OPTION_1\",\"label\":\"Yes\"}]", false)]
	[InlineData("[{\"value\":\"OPTION_1\"}]", false)]
	[InlineData("[\"Yes\"]", false)]
	public void Choice_options_require_a_name_when_a_label_is_provided(string optionsJson, bool invalid)
	{
		var field = new AdmissionFormFieldDefinition(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CUSTOM_RADIO", "Custom radio",
			AdmissionFormFieldType.Radio, AdmissionFormBindingType.Custom, "CUSTOM_RADIO", false, false,
			0, null, null, null, null, null, optionsJson);
		var result = new AdmissionFormValidationResultDto();

		AdmissionFormAppService.ValidateOptions(field, new HashSet<string>(StringComparer.OrdinalIgnoreCase), result);

		if (invalid) result.Errors.ShouldContain("Admissions.FormInvalidOptions:CUSTOM_RADIO");
		else result.Errors.ShouldBeEmpty();
	}
}
