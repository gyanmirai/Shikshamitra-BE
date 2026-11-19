using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFormVersionComparison_Tests
{
	[Theory]
	[InlineData("visibility")]
	[InlineData("requiredRule")]
	[InlineData("validation")]
	[InlineData("binding")]
	[InlineData("readOnly")]
	[InlineData("order")]
	[InlineData("help")]
	[InlineData("default")]
	[InlineData("translation")]
	public void Comparison_reports_question_behavior_changes(string change)
	{
		var before = Definition();
		var after = Copy(before);
		var field = after.Sections[0].Fields[0];
		switch (change)
		{
			case "visibility": field.VisibilityRuleJson = "{\"field\":\"FLAG\",\"operator\":\"IsNotEmpty\"}"; break;
			case "requiredRule": field.RequiredRuleJson = "{\"field\":\"FLAG\",\"operator\":\"IsNotEmpty\"}"; break;
			case "validation": field.ValidationJson = "{\"maxLength\":30}"; break;
			case "binding": field.BindingType = AdmissionFormBindingType.System; field.BindingKey = "Applicant.FirstName"; break;
			case "readOnly": field.ReadOnly = true; break;
			case "order": field.DisplayOrder = 2; break;
			case "help": field.HelpText = "Choose your preferred route."; break;
			case "default": field.DefaultValue = "yes"; break;
			case "translation": field.Translations["hi"] = new() { Label = "परिवहन" }; break;
		}
		Compare(before, after).ShouldContain("Field changed: Transport (Details)");
		before.Sections[0].Fields[0].ReadOnly.ShouldBeFalse();
	}

	[Theory]
	[InlineData("visibility")]
	[InlineData("step")]
	[InlineData("order")]
	[InlineData("columns")]
	[InlineData("translation")]
	public void Comparison_reports_section_behavior_changes(string change)
	{
		var before = Definition();
		var after = Copy(before);
		var section = after.Sections[0];
		switch (change)
		{
			case "visibility": section.VisibilityRuleJson = "{\"field\":\"FLAG\",\"operator\":\"IsNotEmpty\"}"; break;
			case "step": section.StepVisibilityRuleJson = "{\"field\":\"FLAG\",\"operator\":\"IsNotEmpty\"}"; break;
			case "order": section.DisplayOrder = 3; break;
			case "columns": section.FieldsPerRow = 2; break;
			case "translation": section.Translations["hi"] = new() { Label = "विवरण" }; break;
		}
		Compare(before, after).ShouldContain("Section changed: Details");
	}

	[Fact]
	public void Comparison_reports_source_approval_and_default_language_changes()
	{
		var before = Definition();
		var after = Copy(before);
		after.DefaultLanguage = "hi";
		after.Inheritance = new() { Enabled = true };
		Compare(before, after).ShouldContain("Default language changed");
		Compare(before, after).ShouldContain("Inquiry inheritance changed");
	}

	[Fact]
	public void Comparison_ignores_generated_ids_and_equivalent_json_formatting()
	{
		var before = Definition();
		before.Sections[0].Fields[0].ValidationJson = "{\"minLength\":1,\"maxLength\":30}";
		var after = Copy(before);
		after.Id = Guid.NewGuid();
		after.Version = 2;
		after.Status = AdmissionFormStatus.Draft;
		after.Sections[0].Id = Guid.NewGuid();
		after.Sections[0].Fields[0].Id = Guid.NewGuid();
		after.Sections[0].Fields[0].ValidationJson = "{ \"maxLength\": 30, \"minLength\": 1 }";
		Compare(before, after).ShouldBeEmpty();
	}

	[Fact]
	public void Comparison_ignores_remapped_step_guids_but_detects_changed_group_membership()
	{
		var before = Definition();
		var firstStep = Guid.NewGuid();
		before.Sections[0].StepId = firstStep;
		before.Sections.Add(new() { Code = "ADDITIONAL", Name = "Additional", StepId = firstStep });
		before.Sections.Add(new() { Code = "FINISH", Name = "Finish", StepId = Guid.NewGuid() });
		var after = Copy(before);
		var replacementStep = Guid.NewGuid();
		after.Sections[0].StepId = replacementStep;
		after.Sections[1].StepId = replacementStep;
		after.Sections[2].StepId = Guid.NewGuid();
		Compare(before, after).ShouldBeEmpty();
		after.Sections[1].StepId = after.Sections[2].StepId;
		Compare(before, after).ShouldContain("Section changed: Additional");
	}

	[Fact]
	public void Comparison_tracks_legacy_steps_using_a_section_id_as_the_group_key()
	{
		var before = Definition();
		before.Sections[0].Id = Guid.NewGuid();
		before.Sections.Add(new() { Id = Guid.NewGuid(), Code = "ADDITIONAL", Name = "Additional", StepId = before.Sections[0].Id });
		var after = Copy(before);
		after.Sections[0].Id = Guid.NewGuid();
		after.Sections[1].Id = Guid.NewGuid();
		after.Sections[1].StepId = after.Sections[0].Id;
		Compare(before, after).ShouldBeEmpty();
		after.Sections[1].StepId = Guid.NewGuid();
		Compare(before, after).ShouldContain("Section changed: Additional");
	}

	[Fact]
	public void Comparison_uses_both_visible_labels_when_a_field_is_renamed()
	{
		var before = Definition();
		before.Sections[0].Fields[0].Label = "Parent email";
		var original = JsonSerializer.Serialize(before);
		var after = Copy(before);
		after.Sections[0].Fields[0].Label = "Guardian email";
		Compare(before, after).ShouldBe(["Field changed: Parent email → Guardian email (Details)"]);
		JsonSerializer.Serialize(before).ShouldBe(original);
	}

	[Fact]
	public void Comparison_uses_labels_and_section_names_for_added_and_removed_content()
	{
		var before = Definition();
		before.Sections.Add(new() { Code = "LEGACY_INTERNAL", Name = "Old details" });
		var after = Copy(before);
		after.Sections.RemoveAt(1);
		after.Sections.Add(new() { Code = "CONTACT_INTERNAL", Name = "Parent details" });
		after.Sections[0].Fields.RemoveAt(0);
		after.Sections[0].Fields.Add(new() { Code = "GUARDIAN_EMAIL", Label = "Parent email" });
		var changes = Compare(before, after);
		changes.ShouldContain("Section added: Parent details");
		changes.ShouldContain("Section removed: Old details");
		changes.ShouldContain("Field added: Parent email (Details)");
		changes.ShouldContain("Field removed: Transport (Details)");
		string.Join(";", changes).ShouldNotContain("INTERNAL");
		string.Join(";", changes).ShouldNotContain("GUARDIAN_EMAIL");
	}

	[Fact]
	public void Comparison_describes_section_renames_and_repeat_settings_with_visible_names()
	{
		var before = Definition();
		var after = Copy(before);
		after.Sections[0].Name = "Contact details";
		after.Sections[0].IsRepeatable = true;
		var changes = Compare(before, after);
		changes.ShouldContain("Section renamed: Details → Contact details");
		changes.ShouldContain("Repeat settings changed: Contact details");
	}

	[Fact]
	public void Comparison_uses_friendly_fallbacks_for_legacy_missing_labels()
	{
		var before = Definition();
		before.Sections[0].Name = " ";
		before.Sections[0].Fields[0].Label = " ";
		var after = Copy(before);
		after.Sections[0].Fields[0].HelpText = "Updated guidance";
		Compare(before, after).ShouldBe(["Field changed: Unnamed field (Unnamed section)"]);
	}

	[Fact]
	public void Comparison_retains_every_changed_field_when_visible_labels_match()
	{
		var before = Definition();
		before.Sections[0].Fields.ForEach(field => field.Label = "Parent email");
		var after = Copy(before);
		after.Sections[0].Fields.ForEach(field => field.HelpText = "Updated guidance");
		Compare(before, after).ShouldBe(["Field changed: Parent email (Details)", "Field changed: Parent email (Details)"]);
	}

	private static AdmissionFormDto Definition() => new()
	{
		Name = "Inquiry", Code = "INQUIRY", Version = 1,
		Sections = [new() { Code = "DETAILS", Name = "Details", Fields = [new() { Code = "TRANSPORT", Label = "Transport" }, new() { Code = "FLAG", Label = "Use transport" }] }]
	};
	private static AdmissionFormDto Copy(AdmissionFormDto form) => JsonSerializer.Deserialize<AdmissionFormDto>(JsonSerializer.Serialize(form))!;
	private static List<string> Compare(AdmissionFormDto before, AdmissionFormDto after) =>
		(List<string>)typeof(AdmissionFormAppService).GetMethod("Compare", BindingFlags.NonPublic | BindingFlags.Static)!
			.Invoke(null, [before, after])!;
}
