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
}
