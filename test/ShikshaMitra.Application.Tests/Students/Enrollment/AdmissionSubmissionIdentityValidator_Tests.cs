using System;
using System.Collections.Generic;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionSubmissionIdentityValidator_Tests
{
	[Fact]
	public void Complete_inquiry_identity_needs_no_duplicate_registration_questions()
	{
		var result = new AdmissionFormValidationResult([], []);
		AdmissionSubmissionIdentityValidator.Validate(Applicant(), [], new HashSet<string>(), new Dictionary<string, string>(), result);
		result.Errors.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Missing_identity_without_an_editable_question_has_an_actionable_unmapped_error(bool readOnly)
	{
		var field = Field("Applicant.Gender", readOnly);
		var result = new AdmissionFormValidationResult([], []);
		AdmissionSubmissionIdentityValidator.Validate(Applicant(null), [field],
			readOnly ? new HashSet<string> { field.Code } : new HashSet<string>(),
			new Dictionary<string, string> { [field.Code] = "Female" }, result);
		result.Errors.ShouldContain(error => error.Contains("Gender is required") && error.Contains("Update applicant identity"));
		result.FieldErrors.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("Applicant.FirstName", " ")]
	[InlineData("Applicant.DateOfBirth", "not a date")]
	[InlineData("Applicant.Gender", "99")]
	public void A_saved_identity_cannot_hide_a_cleared_or_invalid_visible_answer(string binding, string value)
	{
		var field = Field(binding);
		var result = new AdmissionFormValidationResult([], []);
		AdmissionSubmissionIdentityValidator.Validate(Applicant(), [field], new HashSet<string> { field.Code },
			new Dictionary<string, string> { [field.Code] = value }, result);
		result.FieldErrors.ShouldContainKey(field.Code);
	}

	private static AdmissionApplicant Applicant(string? gender = "Female") => new(Guid.NewGuid(), null, Guid.NewGuid(),
		"Cher", null, null, new DateTime(2016, 1, 1), gender, null, Guid.NewGuid());
	private static AdmissionFormFieldDefinition Field(string binding, bool readOnly = false) => new(Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), "IDENTITY", "Identity answer", AdmissionFormFieldType.Text,
		AdmissionFormBindingType.System, binding, false, readOnly, 0, null, null, null, null, null);
}
