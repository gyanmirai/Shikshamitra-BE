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

	[Fact]
	public void Repeatable_section_validates_each_entry_and_configured_range()
	{
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		var field = Field("SIBLING_NAME", required: true);
		var section = Section(field.SectionId);
		var experience = new AdmissionFormExperienceMetadata
		{
			Sections = new Dictionary<string, AdmissionFormSectionExperienceMetadata>(StringComparer.OrdinalIgnoreCase)
			{
				[section.Code] = new() { IsRepeatable = true, MinEntries = 2, MaxEntries = 3 }
			}
		};

		var missing = validator.Validate([field], [section], new Dictionary<string, string>
		{
			[field.Code] = "[\"Aarav\",\"\"]"
		}, experience);
		missing.FieldErrors.ShouldContainKey("SIBLING_NAME[1]");

		var valid = validator.Validate([field], [section], new Dictionary<string, string>
		{
			[field.Code] = "[\"Aarav\",\"Diya\"]"
		}, experience);
		valid.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Experience_metadata_round_trips_languages_repetition_and_translations()
	{
		var form = new AdmissionFormDefinition(Guid.NewGuid(), null, "Admission", "ADMISSION", AdmissionFormLayoutType.SinglePage);
		var input = new CreateUpdateAdmissionFormDto
		{
			Name = "Admission",
			Code = "ADMISSION",
			DefaultLanguage = "gu",
			SupportedLanguages = ["en", "gu"],
			Sections =
			[
				new CreateUpdateAdmissionFormSectionDto
				{
					Code = "SIBLINGS", Name = "Siblings", IsRepeatable = true, MinEntries = 0, MaxEntries = 5,
					Translations = new Dictionary<string, AdmissionFormTranslationDto>
					{
						["gu"] = new() { Label = "ભાઈ બહેન" }
					}
				}
			]
		};

		AdmissionFormMetadataStore.WriteExperience(form, input);
		var stored = AdmissionFormMetadataStore.ReadExperience(form);

		stored.DefaultLanguage.ShouldBe("gu");
		stored.SupportedLanguages.ShouldContain("en");
		stored.Sections["SIBLINGS"].IsRepeatable.ShouldBeTrue();
		stored.Sections["SIBLINGS"].MaxEntries.ShouldBe(5);
		stored.Sections["SIBLINGS"].Translations["gu"].Label.ShouldBe("ભાઈ બહેન");
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
