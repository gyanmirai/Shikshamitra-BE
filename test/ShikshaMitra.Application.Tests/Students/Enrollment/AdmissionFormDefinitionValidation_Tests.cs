using System;
using System.Collections.Generic;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormDefinitionValidation_Tests
{
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
