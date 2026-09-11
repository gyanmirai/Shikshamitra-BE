using System;
using System.Collections.Generic;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionApplicationFormValidator_Tests
{
	[Fact]
	public void Optional_field_without_required_rule_does_not_become_required()
	{
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		var field = Field("OPTIONAL_FIELD", required: false);

		var result = validator.Validate([field], [Section(field.SectionId)], new Dictionary<string, string>());

		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Required_rule_is_applied_only_when_condition_matches()
	{
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		var field = Field("PREVIOUS_SCHOOL", required: false, requiredRuleJson: "{\"field\":\"HAS_PREVIOUS_SCHOOL\",\"operator\":\"Equals\",\"value\":true}");

		validator.Validate([field], [Section(field.SectionId)], new Dictionary<string, string> { ["HAS_PREVIOUS_SCHOOL"] = "false" }).Errors.ShouldBeEmpty();
		validator.Validate([field], [Section(field.SectionId)], new Dictionary<string, string> { ["HAS_PREVIOUS_SCHOOL"] = "true" }).FieldErrors.ShouldContainKey("PREVIOUS_SCHOOL");
	}

	private static AdmissionFormFieldDefinition Field(string code, bool required, string? requiredRuleJson = null)
	{
		return new AdmissionFormFieldDefinition(Guid.NewGuid(), Guid.NewGuid(), SectionId, code, code,
			AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, code, required, false, 0,
			null, null, null, null, null, null, requiredRuleJson);
	}

	private static Guid SectionId { get; } = Guid.NewGuid();
	private static AdmissionFormSectionDefinition Section(Guid sectionId) => new(sectionId, Guid.NewGuid(), "GENERAL", "General", 0, null);
}
