using System;
using System.Linq;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionCoreFieldPolicy_Tests
{
	[Fact]
	public void Fresh_inquiry_supplies_six_canonical_fields_and_optional_surname()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var fields = draft.Sections.SelectMany(section => section.Fields).ToList();
		fields.Select(field => field.BindingKey).ShouldBe(new[] { "Applicant.FirstName", "Applicant.LastName", "Guardian.FirstName", "Guardian.Mobile", "Guardian.Email", "Inquiry.ParentRelationship" });
		fields.Single(field => field.BindingKey == "Applicant.LastName").Required.ShouldBeFalse();
		fields.Where(field => field.BindingKey != "Applicant.LastName").All(field => field.Required && !field.ReadOnly).ShouldBeTrue();
		AdmissionCoreFieldPolicy.Validate(draft).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("optional")]
	[InlineData("readonly")]
	[InlineData("hidden")]
	[InlineData("repeatable")]
	[InlineData("section-hidden")]
	[InlineData("step-hidden")]
	[InlineData("duplicate")]
	[InlineData("type")]
	[InlineData("surname-required")]
	[InlineData("relationship-options")]
	public void Core_structure_cannot_be_bypassed_by_altered_definition(string mutation)
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var section = draft.Sections.First();
		var email = section.Fields.Single(field => field.BindingKey == "Guardian.Email");
		switch (mutation)
		{
			case "missing": section.Fields.Remove(email); break;
			case "optional": email.Required = false; break;
			case "readonly": email.ReadOnly = true; break;
			case "hidden": email.VisibilityRuleJson = "{}"; break;
			case "repeatable": section.IsRepeatable = true; break;
			case "section-hidden": section.VisibilityRuleJson = "{}"; break;
			case "step-hidden": section.StepVisibilityRuleJson = "{}"; break;
			case "duplicate": section.Fields.Add(email); break;
			case "type": email.FieldType = AdmissionFormFieldType.Text; break;
			case "surname-required": section.Fields.Single(field => field.BindingKey == "Applicant.LastName").Required = true; break;
			case "relationship-options": section.Fields.Single(field => field.BindingKey == "Inquiry.ParentRelationship").OptionsJson = "[{\"value\":\"father\"},{\"value\":\"mother\"}]"; break;
		}
		AdmissionCoreFieldPolicy.Validate(draft).ShouldNotBeEmpty();
	}

	[Fact]
	public void Renamed_core_question_remains_protected_by_binding_and_preserves_translation()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var email = draft.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Guardian.Email");
		email.Label = "School contact email";
		email.Translations["hi"] = new() { Label = "ईमेल" };
		AdmissionCoreFieldPolicy.Validate(draft).ShouldBeEmpty();
	}

	[Fact]
	public void Inheriting_registration_keeps_source_identity_and_adds_only_birth_date_and_gender()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Registration, Inheritance = new() { Enabled = true } };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		draft.Sections.SelectMany(section => section.Fields).Select(field => field.BindingKey).ShouldBe(new[] { "Applicant.DateOfBirth", "Applicant.Gender" });
	}

	[Fact]
	public void New_version_repairs_legacy_optional_core_settings_without_changing_labels()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var section = draft.Sections.Single();
		section.VisibilityRuleJson = "{}";
		var email = section.Fields.Single(field => field.BindingKey == "Guardian.Email");
		email.Required = false; email.ReadOnly = true; email.Label = "Preferred email";
		AdmissionCoreFieldPolicy.UpgradeDraft(draft);
		AdmissionCoreFieldPolicy.Validate(draft).ShouldBeEmpty();
		draft.Sections.SelectMany(item => item.Fields).Single(field => field.BindingKey == "Guardian.Email").Label.ShouldBe("Preferred email");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("invalid-email")]
	public void New_policy_runtime_requires_valid_parent_email(string? email)
	{
		Should.Throw<Volo.Abp.BusinessException>(() => AdmissionCoreFieldPolicy.EnsureParentEmail(1, email));
	}

	[Fact]
	public void Legacy_runtime_keeps_optional_email_and_new_policy_accepts_valid_email()
	{
		AdmissionCoreFieldPolicy.EnsureParentEmail(null, null);
		AdmissionCoreFieldPolicy.EnsureParentEmail(1, "parent@example.com");
	}
	[Fact]
	public void Fresh_definition_reuses_reserved_legacy_parent_codes_and_removes_full_name_alias_without_splitting_answers()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry, Sections = [new() { Code = "LEGACY", Name = "Legacy", Fields = [
			new() { Code = "STUDENT_NAME", Label = "Child name", BindingType = AdmissionFormBindingType.Custom, BindingKey = "STUDENT_NAME", FieldType = AdmissionFormFieldType.Text, Required = true },
			new() { Code = "PARENT_EMAIL", Label = "School email", BindingType = AdmissionFormBindingType.Custom, BindingKey = "PARENT_EMAIL", FieldType = AdmissionFormFieldType.Email }
		] }] };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		AdmissionCoreFieldPolicy.UpgradeDraft(draft);
		var fields = draft.Sections.SelectMany(section => section.Fields).ToList();
		fields.ShouldNotContain(field => field.Code == "STUDENT_NAME");
		fields.Count(field => field.Code.StartsWith("PARENT_EMAIL", StringComparison.Ordinal)).ShouldBe(1);
		fields.Single(field => field.BindingKey == "Guardian.Email").Label.ShouldBe("School email");
		AdmissionCoreFieldPolicy.Validate(draft).ShouldBeEmpty();
	}
	[Fact]
	public void New_version_keeps_custom_choice_labels_when_values_are_already_canonical()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var relationship = draft.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Inquiry.ParentRelationship");
		relationship.OptionsJson = "[{\"value\":\"father\",\"label\":\"Papa\"},{\"value\":\"mother\",\"label\":\"Mummy\"},{\"value\":\"guardian\",\"label\":\"Responsible adult\"}]";
		var original = relationship.OptionsJson;
		AdmissionCoreFieldPolicy.UpgradeDraft(draft);
		relationship.OptionsJson.ShouldBe(original);
	}

	[Fact]
	public void Uppercase_relationship_values_cannot_replace_runtime_canonical_values()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var relationship = draft.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Inquiry.ParentRelationship");
		relationship.OptionsJson = "[{\"value\":\"FATHER\"},{\"value\":\"MOTHER\"},{\"value\":\"GUARDIAN\"}]";
		AdmissionCoreFieldPolicy.Validate(draft).ShouldNotBeEmpty();
	}
	[Theory]
	[InlineData("Applicant.DateOfBirth", AdmissionFormFieldType.Text, null)]
	[InlineData("Applicant.Gender", AdmissionFormFieldType.Text, null)]
	[InlineData("Applicant.Gender", AdmissionFormFieldType.Select, "[{\"value\":\"Unknown\"}]")]
	public void Wrong_type_or_unsupported_choices_cannot_guarantee_source_identity(string key, AdmissionFormFieldType type, string? options)
	{
		var form = new AdmissionFormDto { Sections = [new() { Fields = [new() { BindingType = AdmissionFormBindingType.System, BindingKey = key, Required = true, FieldType = type, OptionsJson = options }] }] };
		AdmissionCoreFieldPolicy.IsGuaranteed(form, key).ShouldBeFalse();
	}
	[Fact]
	public void Core_fields_cannot_be_hidden_by_another_section_in_the_same_step()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var stepId = Guid.NewGuid();
		draft.Sections.Single().StepId = stepId;
		draft.Sections.Add(new() { Code = "HIDDEN_STEP", Name = "Hidden step", StepId = stepId, StepVisibilityRuleJson = "{}" });
		AdmissionCoreFieldPolicy.Validate(draft).ShouldNotBeEmpty();
	}

	[Fact]
	public void New_version_keeps_custom_gender_choice_labels_when_values_are_supported()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Registration };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		var gender = draft.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Applicant.Gender");
		gender.OptionsJson = "[{\"value\":\"Male\",\"label\":\"Boy\"},{\"value\":\"Female\",\"label\":\"Girl\"},{\"value\":\"Other\",\"label\":\"अन्य\"}]";
		var original = gender.OptionsJson;
		AdmissionCoreFieldPolicy.UpgradeDraft(draft);
		gender.OptionsJson.ShouldBe(original);
	}

	[Theory]
	[InlineData(AdmissionFormPurpose.Inquiry, "Applicant.FirstName")]
	[InlineData(AdmissionFormPurpose.Inquiry, "Applicant.LastName")]
	[InlineData(AdmissionFormPurpose.Inquiry, "Guardian.FirstName")]
	[InlineData(AdmissionFormPurpose.Inquiry, "Guardian.Mobile")]
	[InlineData(AdmissionFormPurpose.Inquiry, "Guardian.Email")]
	[InlineData(AdmissionFormPurpose.Inquiry, "Inquiry.ParentRelationship")]
	[InlineData(AdmissionFormPurpose.Registration, "Applicant.DateOfBirth")]
	[InlineData(AdmissionFormPurpose.Registration, "Applicant.Gender")]
	public void Versioned_core_binding_casing_cannot_change_the_runtime_connection(AdmissionFormPurpose purpose, string binding)
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = purpose };
		AdmissionCoreFieldPolicy.PrepareNew(draft);
		draft.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == binding).BindingKey = binding.ToLowerInvariant();
		AdmissionCoreFieldPolicy.Validate(draft).ShouldContain("Admissions.FormCoreFieldProtected:" + binding);
	}

	[Fact]
	public void Legacy_binding_casing_remains_untouched_but_a_new_copy_gets_canonical_connections()
	{
		var legacy = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.PrepareNew(legacy);
		legacy.CoreFieldPolicyVersion = null;
		legacy.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Guardian.Email").BindingKey = "guardian.email";
		AdmissionCoreFieldPolicy.Validate(legacy).ShouldBeEmpty();
		var before = System.Text.Json.JsonSerializer.Serialize(legacy);
		var copy = System.Text.Json.JsonSerializer.Deserialize<CreateUpdateAdmissionFormDto>(before)!;
		AdmissionCoreFieldPolicy.PrepareNew(copy);
		AdmissionCoreFieldPolicy.UpgradeDraft(copy);
		copy.Sections.SelectMany(section => section.Fields).Single(field => field.FieldType == AdmissionFormFieldType.Email).BindingKey.ShouldBe("Guardian.Email");
		AdmissionCoreFieldPolicy.Validate(copy).ShouldBeEmpty();
		System.Text.Json.JsonSerializer.Serialize(legacy).ShouldBe(before);
	}

	[Theory]
	[InlineData("Applicant.DateOfBirth", AdmissionFormFieldType.Date)]
	[InlineData("Applicant.Gender", AdmissionFormFieldType.Select)]
	public void Versioned_registration_cannot_treat_noncanonical_source_binding_as_guaranteed(string binding, AdmissionFormFieldType type)
	{
		var source = new AdmissionFormDto { Sections = [new() { Fields = [new() { BindingType = AdmissionFormBindingType.System, BindingKey = binding.ToLowerInvariant(), Required = true, FieldType = type }] }] };
		AdmissionCoreFieldPolicy.IsGuaranteed(source, binding).ShouldBeFalse();
	}

	[Fact]
	public void Historical_schema_is_not_governed_without_policy_marker()
	{
		var draft = new CreateUpdateAdmissionFormDto { Purpose = AdmissionFormPurpose.Inquiry };
		AdmissionCoreFieldPolicy.Validate(draft).ShouldBeEmpty();
	}
}
