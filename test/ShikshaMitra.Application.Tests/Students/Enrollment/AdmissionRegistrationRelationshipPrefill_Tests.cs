using System;
using System.Collections.Generic;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionRegistrationRelationshipPrefill_Tests
{
	[Theory]
	[InlineData(null)]
	[InlineData("uncle")]
	public void No_role_is_inferred_without_an_explicit_supported_selection(string? role)
	{
		AdmissionRegistrationRelationshipPrefill.Resolve(Field(), Applicant(role)).ShouldBeNull();
	}

	[Theory]
	[InlineData("[{\"value\":\"father\",\"label\":\"Father\"}]", "father")]
	[InlineData("[{\"value\":\"F\",\"label\":\"Father\"}]", "F")]
	[InlineData("[{\"value\":\"mother\",\"label\":\"Mother\"}]", null)]
	[InlineData("[{\"value\":\"x\",\"label\":1}]", null)]
	[InlineData("{\"source\":\"Something\"}", null)]
	[InlineData("invalid", null)]
	public void A_choice_uses_its_configured_value_only_when_the_captured_role_matches(string options, string? expected)
	{
		var field = Field(); field.FieldType = AdmissionFormFieldType.Select; field.OptionsJson = options;
		AdmissionRegistrationRelationshipPrefill.Resolve(field, Applicant("father")).ShouldBe(expected);
	}

	[Fact]
	public void Explicit_inherited_clear_wins_over_the_captured_role()
	{
		var field = Field();
		field.InheritedFrom = new() { SourceBindingKey = "Guardian.Relationship", SourceFieldCode = "RELATION", InquiryFormDefinitionId = Guid.NewGuid() };
		AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant("father"), null,
			new Dictionary<string, string> { ["Guardian.Relationship"] = "\"\"" }).ShouldBe("\"\"");
	}

	[Fact]
	public void An_inherited_null_clear_is_not_resurrected_when_initial_creation_has_no_value_row()
	{
		var field = Field(); field.Label = "Relationship";
		field.InheritedFrom = new() { SourceBindingKey = "Guardian.Relationship", SourceFieldCode = "RELATION", InquiryFormDefinitionId = Guid.NewGuid() };
		var initial = AdmissionApplicationLifecycleService.ResolveFrozenInitialValue(field, Applicant("father"), null,
			new Dictionary<string, string> { ["Guardian.Relationship"] = "null" });
		initial.ShouldBe("null");
		// Initial creation omits literal null rows. A later read must still honor the source-owned clear.
		var result = new AdmissionApplicationFormDto();
		AdmissionRegistrationRelationshipPrefill.Project(Definition(field), Applicant("father"), result,
			new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator()));
		result.Values.ShouldBeEmpty();
		result.AutoFilledFieldCodes.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("\"Mother\"")]
	[InlineData("\"\"")]
	[InlineData("null")]
	public void Projection_preserves_every_stored_answer_including_explicit_clears(string saved)
	{
		var result = new AdmissionApplicationFormDto { Values = [new() { FieldCode = "RELATION", ValueJson = saved }] };
		AdmissionRegistrationRelationshipPrefill.Project(Definition(Field()), Applicant("father"), result, new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator()));
		result.Values.ShouldHaveSingleItem().ValueJson.ShouldBe(saved);
		result.AutoFilledFieldCodes.ShouldBeEmpty();
	}

	[Fact]
	public void Custom_readonly_repeatable_or_hidden_questions_are_not_projected()
	{
		var field = Field(); field.BindingType = AdmissionFormBindingType.Custom;
		AdmissionRegistrationRelationshipPrefill.Resolve(field, Applicant("father")).ShouldBeNull();
		field = Field(); field.ReadOnly = true;
		AdmissionRegistrationRelationshipPrefill.Resolve(field, Applicant("father")).ShouldBeNull();
		field = Field(); field.VisibilityRuleJson = "{\"field\":\"SHOW\",\"operator\":\"Equals\",\"value\":\"yes\"}";
		var result = new AdmissionApplicationFormDto();
		AdmissionRegistrationRelationshipPrefill.Project(Definition(field), Applicant("father"), result, new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator()));
		result.Values.ShouldBeEmpty();
		var repeatable = Definition(Field()); repeatable.Sections[0].IsRepeatable = true;
		AdmissionRegistrationRelationshipPrefill.Project(repeatable, Applicant("father"), result, new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator()));
		result.Values.ShouldBeEmpty();
	}

	private static AdmissionFormDto Definition(AdmissionFormFieldDto field)
	{
		var formId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
		field.Id = Guid.NewGuid(); field.FormDefinitionId = formId; field.SectionId = sectionId;
		return new() { Id = formId, Sections = [new() { Id = sectionId, FormDefinitionId = formId, Code = "FAMILY", Name = "Family", Fields = [field] }] };
	}
	private static AdmissionFormFieldDto Field() => new() { Code = "RELATION", Label = "Relationship", BindingType = AdmissionFormBindingType.System, BindingKey = "Guardian.Relationship", FieldType = AdmissionFormFieldType.Text };
	private static AdmissionApplicant Applicant(string? role)
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QA", null, null, null, null, null, Guid.NewGuid());
		if (role != null) applicant.SetProperty("AdmissionPrimaryContactRelationship", role);
		return applicant;
	}
}
