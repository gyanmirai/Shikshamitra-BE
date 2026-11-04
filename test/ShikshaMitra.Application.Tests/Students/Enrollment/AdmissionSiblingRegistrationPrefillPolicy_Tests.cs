using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionSiblingRegistrationPrefillPolicy_Tests
{
	[Theory]
	[InlineData("captured")]
	[InlineData("alternate")]
	public void Independent_guardian_identity_and_profession_do_not_block_canonical_parent_prefill(string identity)
	{
		var target = Target();
		if (identity == "captured") AdmissionFamilyContactCapture.Capture(target, new() { MainContactRelationship = "mother",
			OtherGuardian = new() { FullName = "Alice", Relationship = "Aunt", Mobile = "9876543212" } }, "mother");
		else target.SetProperty("AdmissionStudentField.Guardian.AlternateName", "Alice");
		target.SetProperty("AdmissionStudentField.Guardian.Occupation", "Nurse");
		var result = Project(target, [Field("PARENT_JOB", "Mother.Occupation"), Field("GENERIC_JOB", "Guardian.Occupation")],
			new() { ["Mother.Name"] = "Asha", ["Mother.Occupation"] = "Teacher" });
		result.AutoFilledFieldCodes.ShouldBe(["PARENT_JOB"]);
		Value(result, "PARENT_JOB").ShouldBe("Teacher");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Partial_source_does_not_assign_selected_parent_profession_to_a_different_or_unassociated_generic_contact(bool capturedSelectedName)
	{
		var target = Target();
		if (capturedSelectedName) target.SetProperty("AdmissionStudentField.Mother.Name", "Asha Verma");
		var result = new AdmissionApplicationFormDto { Values = [new() { FieldCode = "CONTACT", ValueJson = "\"Robert\"" }] };
		AdmissionSiblingRegistrationPrefillPolicy.Merge(target, Definition([Field("CONTACT", "Guardian.FirstName"),
			Field("GENERIC_JOB", "Guardian.Occupation"), Field("MOTHER_JOB", "Mother.Occupation")]), new Dictionary<string, string?>
			{ ["Mother.Occupation"] = "Teacher" }, "mother", new HashSet<string>(), result);
		result.AutoFilledFieldCodes.ShouldBe(["MOTHER_JOB"]);
		Value(result, "CONTACT").ShouldBe("Robert");
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public void Unselected_parent_requires_saved_source_identity_and_overlap_with_captured_target_identity(bool capturedTarget, bool sourceMobile)
	{
		var target = Target();
		if (capturedTarget) target.SetProperty("AdmissionStudentField.Father.Name", "Known father");
		var source = new Dictionary<string, string?> { ["Father.Occupation"] = "Engineer" };
		if (sourceMobile) source["Father.Mobile"] = "9876543210";
		var result = Project(target, [Field("JOB", "Father.Occupation")], source);
		result.Values.ShouldBeEmpty();
	}

	[Fact]
	public void Empty_unselected_target_can_use_saved_source_parent_identity_and_profession()
	{
		var result = Project(Target(), [Field("NAME", "Father.Name"), Field("JOB", "Father.Occupation")],
			new() { ["Father.Name"] = "Known father", ["Father.Occupation"] = "Engineer" });
		Value(result, "NAME").ShouldBe("Known father"); Value(result, "JOB").ShouldBe("Engineer");
	}

	[Fact]
	public void Distinct_saved_generic_contact_never_receives_the_selected_parents_profession()
	{
		var result = new AdmissionApplicationFormDto { Values = [new() { FieldCode = "CONTACT", ValueJson = "\"Different\"" }] };
		AdmissionSiblingRegistrationPrefillPolicy.Merge(Target(), Definition([Field("CONTACT", "Guardian.FirstName"),
			Field("GENERIC_JOB", "Guardian.Occupation"), Field("MOTHER_JOB", "Mother.Occupation")]), new Dictionary<string, string?>
			{ ["Mother.Name"] = "Asha Verma", ["Mother.Occupation"] = "Teacher" }, "mother", new HashSet<string>(), result);
		result.AutoFilledFieldCodes.ShouldBe(["MOTHER_JOB"]);
		Value(result, "CONTACT").ShouldBe("Different");
	}

	[Fact]
	public void Confirmed_mother_maps_generic_profession_only_to_mother_and_preserves_distinct_father_group()
	{
		var target = Target();
		target.SetProperty("AdmissionStudentField.Father.Name", "Different father");
		var result = Project(target, [Field("FATHER_NAME", "Father.Name"), Field("FATHER_JOB", "Father.Occupation"),
			Field("MOTHER_JOB", "Mother.Occupation"), Field("GENERIC_JOB", "Guardian.Occupation")],
			new() { ["Father.Name"] = "Original father", ["Father.Occupation"] = "Engineer", ["Mother.Occupation"] = "Teacher" });
		result.AutoFilledFieldCodes.ShouldBe(["MOTHER_JOB", "GENERIC_JOB"]);
		Value(result, "GENERIC_JOB").ShouldBe("Teacher");
		AdmissionStudentProfileMapping.Read(target, "Mother.Occupation").ShouldBeNull();
	}

	[Theory]
	[InlineData("Name", "Different mother", "Original mother")]
	[InlineData("Mobile", "9876543210", "9876543211")]
	[InlineData("Email", "new@example.test", "old@example.test")]
	public void Any_conflicting_identifier_blocks_the_whole_parent_group(string key, string captured, string source)
	{
		var target = Target(); target.SetProperty("AdmissionStudentField.Mother." + key, captured);
		var result = Project(target, [Field("JOB", "Mother.Occupation"), Field("GENERIC", "Guardian.Occupation")],
			new() { ["Mother." + key] = source, ["Mother.Occupation"] = "Teacher" });
		result.Values.ShouldBeEmpty();
	}

	[Fact]
	public void Identical_normalized_contacts_do_not_block_available_profile()
	{
		var target = Target(); target.SetProperty("AdmissionStudentField.Mother.Mobile", "+91 98765-43210");
		var result = Project(target, [Field("JOB", "Mother.Occupation")], new() { ["Mother.Mobile"] = "9876543210", ["Mother.Occupation"] = "Teacher" });
		Value(result, "JOB").ShouldBe("Teacher");
	}

	[Fact]
	public void Generic_profession_alias_cannot_restore_an_authored_parent_profession_clear()
	{
		var result = new AdmissionApplicationFormDto { Values = [new() { FieldCode = "MOTHER_JOB", ValueJson = "\"\"" }] };
		AdmissionSiblingRegistrationPrefillPolicy.Merge(Target(), Definition([Field("MOTHER_JOB", "Mother.Occupation"), Field("GENERIC_JOB", "Guardian.Occupation")]),
			new Dictionary<string, string?> { ["Mother.Occupation"] = "Teacher" }, "mother", new HashSet<string> { "MOTHER_JOB" }, result);
		result.AutoFilledFieldCodes.ShouldBeEmpty();
		Value(result, "MOTHER_JOB").ShouldBeEmpty();
	}

	[Fact]
	public void Saved_values_and_authored_clears_remain_unchanged_while_untouched_empty_fields_fill()
	{
		var result = new AdmissionApplicationFormDto { Values = [new() { FieldCode = "SAVED", ValueJson = "\"Own job\"" },
			new() { FieldCode = "CLEARED", ValueJson = "\"\"" }, new() { FieldCode = "DEFAULT", ValueJson = "null" }] };
		var fields = new[] { Field("SAVED", "Mother.Occupation"), Field("CLEARED", "Mother.Qualification"), Field("DEFAULT", "Mother.Employer") };
		AdmissionSiblingRegistrationPrefillPolicy.Merge(Target(), Definition(fields), new Dictionary<string, string?>
			{ ["Mother.Occupation"] = "Teacher", ["Mother.Qualification"] = "MA", ["Mother.Employer"] = "School" }, "mother",
			new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CLEARED" }, result);
		Value(result, "SAVED").ShouldBe("Own job"); Value(result, "CLEARED").ShouldBeEmpty(); Value(result, "DEFAULT").ShouldBe("School");
		result.AutoFilledFieldCodes.ShouldBe(["DEFAULT"]);
	}

	[Fact]
	public void Read_only_custom_child_address_and_repeatable_fields_are_never_copied()
	{
		var fields = new[] { Field("FROZEN", "Mother.Name", readOnly: true), Field("CUSTOM", "Mother.Occupation", custom: true),
			Field("CHILD", "Applicant.PlaceOfBirth"), Field("ADDRESS", "Address.City") };
		var definition = Definition(fields);
		definition.Sections.Add(new() { Code = "ROWS", IsRepeatable = true, Fields = [Field("ROW", "Mother.Employer")] });
		var result = new AdmissionApplicationFormDto();
		AdmissionSiblingRegistrationPrefillPolicy.Merge(Target(), definition, new Dictionary<string, string?>
			{ ["Mother.Name"] = "Mother", ["Mother.Occupation"] = "Teacher", ["Mother.Employer"] = "School", ["Applicant.PlaceOfBirth"] = "City", ["Address.City"] = "City" },
			"mother", new HashSet<string>(), result);
		result.Values.ShouldBeEmpty();
	}

	[Fact]
	public void Partial_source_leaves_unavailable_fields_empty_and_uses_selected_generic_name()
	{
		var result = Project(Target(), [Field("FIRST", "Guardian.FirstName"), Field("LAST", "Guardian.LastName"),
			Field("JOB", "Mother.Occupation"), Field("DEGREE", "Mother.Qualification")], new() { ["Mother.Name"] = "Asha Verma", ["Mother.Qualification"] = "MA" });
		Value(result, "FIRST").ShouldBe("Asha"); Value(result, "LAST").ShouldBe("Verma"); Value(result, "DEGREE").ShouldBe("MA");
		result.Values.ShouldNotContain(value => value.FieldCode == "JOB");
	}

	[Theory]
	[InlineData("field")]
	[InlineData("section")]
	[InlineData("step")]
	[InlineData("choice")]
	[InlineData("type")]
	public void Suggestions_respect_target_visibility_types_and_configured_choices(string restriction)
	{
		var formId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
		var field = Field("JOB", "Mother.Occupation"); field.Id = Guid.NewGuid(); field.FormDefinitionId = formId;
		field.SectionId = sectionId; field.Label = "Job";
		var section = new AdmissionFormSectionDto { Id = sectionId, FormDefinitionId = formId, Code = "PARENTS", Name = "Parents", Fields = [field] };
		const string hidden = "{\"field\":\"SHOW\",\"operator\":\"Equals\",\"value\":\"yes\"}";
		if (restriction == "field") field.VisibilityRuleJson = hidden;
		if (restriction == "section") section.VisibilityRuleJson = hidden;
		if (restriction == "step") section.StepVisibilityRuleJson = hidden;
		if (restriction == "choice") { field.FieldType = AdmissionFormFieldType.Select; field.OptionsJson = "[\"Engineer\"]"; }
		if (restriction == "type") field.FieldType = AdmissionFormFieldType.Checkbox;
		var result = new AdmissionApplicationFormDto { Values = [new() { FieldCode = "JOB", ValueJson = "\"Teacher\"" }], AutoFilledFieldCodes = ["JOB"] };
		AdmissionSiblingRegistrationPrefillPolicy.Constrain(new() { Id = formId, Sections = [section] }, [], result,
			new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator()));
		result.AutoFilledFieldCodes.ShouldBeEmpty(); result.Values.ShouldBeEmpty();
	}

	private static AdmissionApplicant Target() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Child", null, null, null, null, null, Guid.NewGuid());
	private static AdmissionFormFieldDto Field(string code, string key, bool readOnly = false, bool custom = false) => new()
		{ Code = code, BindingKey = key, BindingType = custom ? AdmissionFormBindingType.Custom : AdmissionFormBindingType.System, ReadOnly = readOnly };
	private static AdmissionFormDto Definition(IEnumerable<AdmissionFormFieldDto> fields) => new() { Sections = [new() { Code = "PARENTS", Fields = fields.ToList() }] };
	private static AdmissionApplicationFormDto Project(AdmissionApplicant target, IEnumerable<AdmissionFormFieldDto> fields, Dictionary<string, string?> source)
	{
		var result = new AdmissionApplicationFormDto();
		AdmissionSiblingRegistrationPrefillPolicy.Merge(target, Definition(fields), source, "mother", new HashSet<string>(), result);
		return result;
	}
	private static string Value(AdmissionApplicationFormDto result, string code) => AdmissionApplicationValueConverter.Unwrap(result.Values.Single(value => value.FieldCode == code).ValueJson);
}
