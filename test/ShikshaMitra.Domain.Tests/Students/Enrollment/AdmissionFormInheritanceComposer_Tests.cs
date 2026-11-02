using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Shouldly;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionFormInheritanceComposer_Tests
{
	[Fact]
	public void Class_interest_alias_is_reused_but_final_applied_class_remains_separate()
	{
		var source = Custom("CLASS_INTERESTED", "Class interested in", "source-class");
		source.FieldType = AdmissionFormFieldType.Select;
		source.OptionsJson = "{\"source\":\"Standards\"}";
		source.Required = true;
		var target = Custom("FIELD_99", "Interested Class", "other-class");
		target.FieldType = AdmissionFormFieldType.Select;
		target.OptionsJson = source.OptionsJson;
		var registration = Form(target, Custom("CLASS_APPLYING_FOR", "Class applying for", "applied-class"));
		var inquiry = Form(source);
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		var fields = result.Definition.Sections.SelectMany(section => section.Fields).ToList();
		fields.Count.ShouldBe(2);
		fields.Single(field => field.InheritedFrom != null).Id.ShouldBe(target.Id);
		fields.Single(field => field.InheritedFrom != null).Required.ShouldBeTrue();
		fields.ShouldContain(field => field.Code == "CLASS_APPLYING_FOR");
	}

	[Fact]
	public void Legacy_source_class_duplicates_block_new_composition_without_mutating_the_source()
	{
		var inquiry = Form(Custom("CLASS_INTERESTED", "Class interested in", "class-one"), Custom("FIELD_1", "Interested Class", "class-two"));
		var before = JsonSerializer.Serialize(inquiry);
		AdmissionFormInheritanceComposer.Compose(Form(), inquiry, Policy(inquiry.Id)).Issues.ShouldContain(issue => issue.Code == "DuplicateSourceIdentity");
		JsonSerializer.Serialize(inquiry).ShouldBe(before);
	}

	[Fact]
	public void Static_class_information_does_not_replace_the_real_class_question()
	{
		var source = Custom("CLASS_INTERESTED", "Class interested in", "real-class");
		var heading = Custom("HEADING", "Interested Class", "heading");
		heading.FieldType = AdmissionFormFieldType.Static;
		var inquiry = Form(source);
		var result = AdmissionFormInheritanceComposer.Compose(Form(heading), inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.SelectMany(section => section.Fields).Count().ShouldBe(2);
	}
	[Fact]
	public void Recommended_registration_reuses_legacy_starter_questions_in_the_original_inquiry_sections_first()
	{
		var registration = Form(Field("REG_DOB", "Birth date", "Applicant.DateOfBirth"),
			Custom("GUARDIAN_NAME", "Parent or guardian name", "registration-parent"), Custom("SCHOOL", "Previous school", "school"));
		registration.LayoutType = AdmissionFormLayoutType.Stepper;
		registration.Sections[0].StepId = Guid.NewGuid();
		registration.Sections[0].StepName = "Registration";
		registration.Sections[0].Fields[0].Required = true;
		var dob = Custom("DATE_OF_BIRTH", "Date of birth", "inquiry-birth");
		dob.FieldType = AdmissionFormFieldType.Date;
		var inquiry = Form(dob, Custom("GUARDIAN_NAME", "Parent or guardian name", "inquiry-parent"));
		inquiry.LayoutType = AdmissionFormLayoutType.Stepper;
		inquiry.FieldsPerRow = 3;
		inquiry.Sections[0].StepId = Guid.NewGuid();
		inquiry.Sections[0].StepName = "Inquiry";
		inquiry.Sections[0].Name = "Original Inquiry section";
		var before = JsonSerializer.Serialize(new[] { registration, inquiry });

		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));

		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.Select(section => section.Name).ShouldBe(new[] { "Original Inquiry section", "Details" });
		var source = result.Definition.Sections[0];
		source.FieldsPerRow.ShouldBe(3);
		source.StepDisplayOrder.ShouldBe(0);
		source.Fields.Select(field => field.Label).ShouldBe(new[] { "Date of birth", "Parent or guardian name" });
		source.Fields[0].Id.ShouldBe(registration.Sections[0].Fields[0].Id);
		source.Fields[0].Required.ShouldBeFalse();
		source.Fields[0].BindingKey.ShouldBe("Applicant.DateOfBirth");
		source.Fields[0].InheritedFrom!.SourceBindingKey.ShouldBe("DATE_OF_BIRTH");
		source.Fields[0].InheritedFrom!.SourceFieldIdentity.ShouldBe("custom:inquiry-birth");
		result.Definition.Sections[1].Fields.Single().Code.ShouldBe("SCHOOL");
		JsonSerializer.Serialize(new[] { registration, inquiry }).ShouldBe(before);
	}

	[Fact]
	public void Source_requirements_rules_and_location_ignore_registration_overrides_and_drop_empty_sections()
	{
		var registration = Form(Field("DATE", "Registration date", "Applicant.DateOfBirth"));
		registration.Sections[0].Fields[0].Required = true;
		var source = Field("DOB", "DOB", "Applicant.DateOfBirth");
		source.ValidationJson = "{\"min\":\"2000-01-01\"}";
		var inquiry = Form(source);
		var policy = Policy(inquiry.Id);
		policy.Sources[0].RequiredFieldIdentities.Add("system:APPLICANT.DATEOFBIRTH");
		policy.Sources[0].Layouts.Add(new() { FieldIdentity = "system:APPLICANT.DATEOFBIRTH", TargetSectionCode = "DETAILS", DisplayOrder = 99 });

		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, policy);

		result.Issues.ShouldBeEmpty();
		var section = result.Definition.Sections.Single();
		section.Id.ShouldNotBe(registration.Sections[0].Id);
		var field = section.Fields.Single();
		field.Required.ShouldBeFalse();
		field.DisplayOrder.ShouldBe(source.DisplayOrder);
		field.ValidationJson.ShouldBe(source.ValidationJson);
	}

	[Fact]
	public void Full_inquiry_name_stays_a_readonly_reference_without_replacing_structured_registration_names()
	{
		var first = Field("FIRST", "First name", "Applicant.FirstName");
		first.FieldType = AdmissionFormFieldType.Text;
		var last = Field("LAST", "Last name", "Applicant.LastName");
		last.FieldType = AdmissionFormFieldType.Text;
		var full = Field("FULL", "Student name", "Applicant.Name");
		full.FieldType = AdmissionFormFieldType.Text;
		full.Required = true;
		full.RequiredRuleJson = "{\"operator\":\"ALL\",\"conditions\":[]}";
		var inquiry = Form(full);
		var result = AdmissionFormInheritanceComposer.Compose(Form(first, last), inquiry, Policy(inquiry.Id));

		result.Issues.ShouldBeEmpty();
		var reference = result.Definition.Sections[0].Fields.Single();
		reference.BindingKey.ShouldBe("Applicant.Name");
		reference.ReadOnly.ShouldBeTrue();
		reference.Required.ShouldBeFalse();
		reference.RequiredRuleJson.ShouldBeNull();
		reference.InheritedFrom!.SourceRequired.ShouldBeTrue();
		result.Definition.Sections[1].Fields.Select(field => field.BindingKey).ShouldBe(new[] { "Applicant.FirstName", "Applicant.LastName" });
	}

	[Fact]
	public void Starter_alias_does_not_override_an_unrelated_explicit_binding()
	{
		var question = Custom("DOB", "Date of birth", "unrelated-custom");
		question.BindingKey = "UNRELATED_QUESTION";
		question.FieldType = AdmissionFormFieldType.Date;
		var inquiry = Form(question);
		var result = AdmissionFormInheritanceComposer.Compose(Form(Field("BIRTH", "Date of birth", "Applicant.DateOfBirth")), inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.SelectMany(section => section.Fields).Count().ShouldBe(2);
	}

	[Fact]
	public void Empty_registration_validation_does_not_create_a_conflict_with_an_unconstrained_source()
	{
		var target = Field("DATE", "Date", "Applicant.DateOfBirth");
		target.ValidationJson = "{}";
		var inquiry = Form(Field("BIRTH", "Birth", "Applicant.DateOfBirth"));
		var result = AdmissionFormInheritanceComposer.Compose(Form(target), inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.SelectMany(section => section.Fields).Single().ValidationJson.ShouldBeNull();
	}

	[Fact]
	public void Newly_added_empty_registration_steps_survive_composition_after_inquiry_steps()
	{
		var registration = Form();
		registration.LayoutType = AdmissionFormLayoutType.Stepper;
		var empty = registration.Sections[0];
		empty.StepId = Guid.NewGuid();
		empty.StepName = "Additional details";
		empty.StepDisplayOrder = 4;
		var inquiry = Form(Custom("INTEREST", "Interests", "interests"));
		inquiry.LayoutType = AdmissionFormLayoutType.Stepper;
		inquiry.Sections[0].StepId = Guid.NewGuid();
		inquiry.Sections[0].StepName = "Inquiry";

		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));

		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.Select(section => section.StepName).ShouldBe(new[] { "Inquiry", "Additional details" });
		result.Definition.Sections[1].Id.ShouldBe(empty.Id);
		result.Definition.Sections[1].Fields.ShouldBeEmpty();
		result.Definition.Sections[1].StepDisplayOrder.ShouldBe(1);
	}

	[Fact]
	public void An_incompatible_legacy_starter_type_is_reported_instead_of_silently_retyping_the_inquiry()
	{
		var source = Custom("DOB", "Date of birth", "text-birth");
		var inquiry = Form(source);
		var result = AdmissionFormInheritanceComposer.Compose(Form(Field("DATE", "Date", "Applicant.DateOfBirth")), inquiry, Policy(inquiry.Id));
		result.Issues.ShouldContain(issue => issue.Code == "FieldTypeConflict");
		result.Definition.Sections.SelectMany(section => section.Fields).Single().FieldType.ShouldBe(AdmissionFormFieldType.Text);
		source.FieldType.ShouldBe(AdmissionFormFieldType.Text);
	}

	[Fact]
	public void Switching_between_approved_sources_reuses_only_the_selected_inquiry_version()
	{
		var registration = Form(Field("REG_DATE", "Registration birth", "Applicant.DateOfBirth"));
		var legacyBirth = Custom("DOB", "Inquiry A birth", "legacy-birth");
		legacyBirth.FieldType = AdmissionFormFieldType.Date;
		var a = Form(legacyBirth);
		var b = Form(Field("BIRTH", "Inquiry B birth", "Applicant.DateOfBirth"));
		var policy = Policy(a.Id);
		policy.Sources.Add(new() { InquiryFormDefinitionId = b.Id });

		var fromA = AdmissionFormInheritanceComposer.Compose(registration, a, policy).Definition.Sections.SelectMany(section => section.Fields).Single();
		var fromB = AdmissionFormInheritanceComposer.Compose(registration, b, policy).Definition.Sections.SelectMany(section => section.Fields).Single();

		fromA.Label.ShouldBe("Inquiry A birth");
		fromA.InheritedFrom!.InquiryFormDefinitionId.ShouldBe(a.Id);
		fromB.Label.ShouldBe("Inquiry B birth");
		fromB.InheritedFrom!.InquiryFormDefinitionId.ShouldBe(b.Id);
		registration.Sections[0].Fields.Single().Label.ShouldBe("Registration birth");
	}
	[Theory]
	[InlineData(AdmissionFormLayoutType.SinglePage, AdmissionFormLayoutType.SinglePage)]
	[InlineData(AdmissionFormLayoutType.SinglePage, AdmissionFormLayoutType.Stepper)]
	[InlineData(AdmissionFormLayoutType.Stepper, AdmissionFormLayoutType.SinglePage)]
	[InlineData(AdmissionFormLayoutType.Stepper, AdmissionFormLayoutType.Stepper)]
	public void Composition_preserves_source_page_grouping_and_columns(
		AdmissionFormLayoutType registrationLayout, AdmissionFormLayoutType inquiryLayout)
	{
		var registration = Form(Custom("SCHOOL", "Previous school", "school"));
		registration.LayoutType = registrationLayout;
		registration.FieldsPerRow = 2;
		var inquiry = Form(Custom("INTEREST", "Learning interests", "interest"));
		inquiry.LayoutType = inquiryLayout;
		inquiry.FieldsPerRow = 4;
		var first = inquiry.Sections[0];
		first.StepId = Guid.NewGuid();
		first.StepCode = "STUDENT";
		first.StepName = "Student";
		first.StepDisplayOrder = 0;
		inquiry.Sections.Add(new()
		{
			Id = Guid.NewGuid(), Code = "CONTACT", Name = "Contact", DisplayOrder = 1,
			StepId = Guid.NewGuid(), StepCode = "CONTACT", StepName = "Contact", StepDisplayOrder = 1,
			Fields = [Custom("PHONE", "Contact number", "phone")]
		});
		inquiry.Sections.Add(new()
		{
			Id = Guid.NewGuid(), Code = "MORE", Name = "More student details", DisplayOrder = 2,
			StepId = first.StepId, StepCode = first.StepCode, StepName = first.StepName, StepDisplayOrder = 0,
			Fields = [Custom("HOBBY", "Hobby", "hobby")]
		});
		var before = JsonSerializer.Serialize(new[] { registration, inquiry });
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		result.Definition.LayoutType.ShouldBe(registrationLayout == AdmissionFormLayoutType.Stepper || inquiryLayout == AdmissionFormLayoutType.Stepper
			? AdmissionFormLayoutType.Stepper : AdmissionFormLayoutType.SinglePage);
		result.Definition.FieldsPerRow.ShouldBe(2);
		var inherited = result.Definition.Sections.Where(section => section.Fields.Any(field => field.InheritedFrom != null)).ToList();
		inherited.Count.ShouldBe(3);
		foreach (var section in inherited)
			JsonSerializer.SerializeToElement(section).GetProperty("FieldsPerRow").GetInt32().ShouldBe(4);
		if (inquiryLayout == AdmissionFormLayoutType.SinglePage)
			inherited.Select(section => section.StepId).Distinct().Count().ShouldBe(1);
		else
		{
			inherited.Select(section => section.StepName).ShouldBe(new[] { "Student", "Contact", "Student" });
			inherited[0].StepId.ShouldBe(inherited[2].StepId);
		}
		JsonSerializer.Serialize(new[] { registration, inquiry }).ShouldBe(before);
	}

	[Fact]
	public void Single_page_inquiry_does_not_apply_dormant_step_rules_to_a_shared_question()
	{
		var registration = Form(Field("DATE", "Birth date", "Applicant.DateOfBirth"));
		var inquiry = Form(Field("BIRTH", "DOB", "Applicant.DateOfBirth"));
		inquiry.Sections[0].StepVisibilityRuleJson = "{\"operator\":\"ANY\",\"conditions\":[]}";
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.SelectMany(section => section.Fields).Single().VisibilityRuleJson.ShouldBeNull();
	}

	[Theory]
	[InlineData("Date of birth")]
	[InlineData("DOB")]
	public void Inherited_label_replaces_registration_label_without_duplicating_shared_identity(string label)
	{
		var registration = Form(Field("DATE", "Registration birth date", "Applicant.DateOfBirth"));
		var inquiry = Form(Field("BIRTH", label, "Applicant.DateOfBirth"));
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));

		result.Issues.ShouldBeEmpty();
		var field = result.Definition.Sections.SelectMany(section => section.Fields).Single();
		field.Label.ShouldBe(label);
		field.Code.ShouldBe("DATE");
		field.InheritedFrom!.SourceFieldCode.ShouldBe("BIRTH");
		registration.Sections.Single().Fields.Single().Label.ShouldBe("Registration birth date");
	}

	[Fact]
	public void Source_only_questions_stay_separate_while_registration_questions_are_shared()
	{
		var registration = Form(Custom("PREVIOUS_SCHOOL", "Previous school", "school-question"));
		var inquiryA = Form(Custom("TRANSPORT", "Transport required?", "transport-question"));
		var inquiryB = Form(Custom("MARKS", "Previous class marks", "marks-question"));

		var a = AdmissionFormInheritanceComposer.Compose(registration, inquiryA, Policy(inquiryA.Id));
		var b = AdmissionFormInheritanceComposer.Compose(registration, inquiryB, Policy(inquiryB.Id));

		a.Definition.Sections.SelectMany(section => section.Fields).Select(field => field.Label)
			.ShouldBe(new[] { "Transport required?", "Previous school" });
		b.Definition.Sections.SelectMany(section => section.Fields).Select(field => field.Label)
			.ShouldBe(new[] { "Previous class marks", "Previous school" });
	}

	[Fact]
	public void Same_custom_label_and_code_do_not_establish_shared_identity()
	{
		var registration = Form(Custom("OTHER", "Other details", "registration-question"));
		var inquiry = Form(Custom("OTHER", "Other details", "inquiry-question"));
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));

		result.Definition.Sections.SelectMany(section => section.Fields).Count().ShouldBe(2);
		result.Definition.Sections.SelectMany(section => section.Fields).Select(field => field.Code)
			.Distinct(StringComparer.OrdinalIgnoreCase).Count().ShouldBe(2);
	}

	[Fact]
	public void Colliding_source_codes_rewrite_rules_and_dependent_lookup_references()
	{
		var registration = Form(Custom("ANSWER", "Registration answer", "registration-answer"));
		var sourceAnswer = Custom("ANSWER", "Inquiry answer", "inquiry-answer");
		var dependent = Custom("DEPENDENT", "Dependent question", "dependent");
		dependent.VisibilityRuleJson = "{\"field\":\"ANSWER\",\"operator\":\"Equals\",\"value\":\"Yes\"}";
		dependent.OptionsJson = "{\"source\":\"cities\",\"parentField\":\"ANSWER\"}";
		var inquiry = Form(sourceAnswer, dependent);
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		var fields = result.Definition.Sections.SelectMany(section => section.Fields).ToList();
		var inheritedCode = fields.Single(field => field.Label == "Inquiry answer").Code;
		var dependentResult = fields.Single(field => field.Label == "Dependent question");

		inheritedCode.ShouldNotBe("ANSWER");
		dependentResult.VisibilityRuleJson.ShouldNotBeNull().ShouldContain(inheritedCode);
		dependentResult.OptionsJson.ShouldNotBeNull().ShouldContain(inheritedCode);
	}

	[Fact]
	public void Registration_cannot_strengthen_source_requiredness_or_change_a_readonly_question()
	{
		var source = Field("DOB", "DOB", "Applicant.DateOfBirth");
		var inquiry = Form(source);
		var policy = Policy(inquiry.Id);
		policy.Sources.Single().RequiredFieldIdentities.Add("system:APPLICANT.DATEOFBIRTH");
		var result = AdmissionFormInheritanceComposer.Compose(Form(), inquiry, policy);
		result.Definition.Sections.SelectMany(section => section.Fields).Single().Required.ShouldBeFalse();

		source.ReadOnly = true;
		var readonlyResult = AdmissionFormInheritanceComposer.Compose(Form(), inquiry, policy);
		readonlyResult.Issues.ShouldBeEmpty();
		readonlyResult.Definition.Sections.SelectMany(section => section.Fields).Single().ReadOnly.ShouldBeTrue();
		readonlyResult.Definition.Sections.SelectMany(section => section.Fields).Single().Required.ShouldBeFalse();
	}

	[Fact]
	public void Inquiry_only_bindings_are_readonly_context_not_editable_registration_fields()
	{
		var inquiry = Form(Field("SOURCE", "How did you hear about us?", "Inquiry.Source"));
		var result = AdmissionFormInheritanceComposer.Compose(Form(), inquiry, Policy(inquiry.Id));

		result.Definition.Sections.SelectMany(section => section.Fields).ShouldBeEmpty();
		result.InquiryContextSections.SelectMany(section => section.Fields).Single().ReadOnly.ShouldBeTrue();
	}

	[Fact]
	public void Unknown_source_version_is_an_issue_and_never_substitutes_another_inquiry()
	{
		var inquiry = Form(Field("DOB", "DOB", "Applicant.DateOfBirth"));
		var result = AdmissionFormInheritanceComposer.Compose(Form(), inquiry, Policy(Guid.NewGuid()));
		result.Issues.ShouldContain(issue => issue.Code == "SourceNotApproved");
	}

	[Fact]
	public void Source_visibility_repeatable_structure_and_translations_are_retained()
	{
		var inquiry = Form(Custom("NAME", "Sibling name", "sibling-name"));
		var section = inquiry.Sections.Single();
		section.IsRepeatable = true;
		section.MaxEntries = 4;
		section.Fields.Single().Translations["gu"] = new() { Label = "ભાઈ બહેનનું નામ" };
		var result = AdmissionFormInheritanceComposer.Compose(Form(), inquiry, Policy(inquiry.Id));
		var inherited = result.Definition.Sections.Single(section => section.Fields.Count > 0);

		inherited.IsRepeatable.ShouldBeTrue();
		inherited.MaxEntries.ShouldBe(4);
		inherited.Fields.Single().Translations["gu"].Label.ShouldBe("ભાઈ બહેનનું નામ");
	}

	private static AdmissionFormSchema Form(params AdmissionFormSchemaField[] fields)
	{
		var id = Guid.NewGuid();
		var section = new AdmissionFormSchemaSection { Id = Guid.NewGuid(), FormDefinitionId = id, Code = "DETAILS", Name = "Details", Fields = fields.ToList() };
		foreach (var field in fields) { field.Id = Guid.NewGuid(); field.FormDefinitionId = id; field.SectionId = section.Id; }
		return new AdmissionFormSchema { Id = id, Name = "Form", Sections = [section] };
	}

	private static AdmissionFormSchemaField Field(string code, string label, string binding) => new()
	{ Code = code, Label = label, BindingType = AdmissionFormBindingType.System, BindingKey = binding, FieldType = AdmissionFormFieldType.Date };

	private static AdmissionFormSchemaField Custom(string code, string label, string identity) => new()
	{ Code = code, Label = label, FieldIdentity = "custom:" + identity, BindingType = AdmissionFormBindingType.Custom, BindingKey = code, FieldType = AdmissionFormFieldType.Text };

	private static AdmissionFormInheritancePolicy Policy(Guid source) => new()
	{ Enabled = true, Sources = [new() { InquiryFormDefinitionId = source }] };

	[Fact]
	public void Duplicate_source_identities_report_issues_instead_of_throwing_after_replacement()
	{
		var registration = Form(Field("DATE", "Date", "Applicant.DateOfBirth"));
		var inquiry = Form(Field("DOB", "DOB", "Applicant.DateOfBirth"), Field("BIRTH", "Birth", "Applicant.DateOfBirth"));
		AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id)).Issues
			.ShouldContain(issue => issue.Code == "DuplicateSourceIdentity");
	}

	[Fact]
	public void A_registration_field_visibility_rule_is_not_silently_discarded()
	{
		var target = Field("DATE", "Date", "Applicant.DateOfBirth");
		target.VisibilityRuleJson = "{\"field\":\"OTHER\",\"operator\":\"IsNotEmpty\"}";
		var registration = Form(target, Custom("OTHER", "Other", "other"));
		var inquiry = Form(Field("DOB", "DOB", "Applicant.DateOfBirth"));
		AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id)).Issues
			.ShouldContain(issue => issue.Code == "VisibilityConflict");
	}

	[Fact]
	public void Choice_labels_and_order_can_differ_while_the_same_option_values_are_compatible()
	{
		var target = Custom("TRANSPORT", "Transport", "transport");
		target.FieldType = AdmissionFormFieldType.Select;
		target.OptionsJson = "[{\"value\":\"BUS\",\"label\":\"School bus\"},{\"value\":\"OWN\",\"label\":\"Own vehicle\"}]";
		var source = Custom("TRAVEL", "Travel", "transport");
		source.FieldType = AdmissionFormFieldType.Select;
		source.OptionsJson = "[{\"value\":\"OWN\",\"label\":\"Self\"},{\"value\":\"BUS\",\"label\":\"Bus\"}]";
		var inquiry = Form(source);
		var result = AdmissionFormInheritanceComposer.Compose(Form(target), inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.SelectMany(section => section.Fields).Single().OptionsJson.ShouldNotBeNull().ShouldContain("Self");
	}

	[Fact]
	public void Missing_translation_uses_source_default_not_registration_default()
	{
		var registration = Form(Field("DATE", "Date of birth", "Applicant.DateOfBirth"));
		registration.DefaultLanguage = "en";
		registration.SupportedLanguages = ["en", "hi"];
		var source = Field("DOB", "Date", "Applicant.DateOfBirth");
		source.Translations["gu"] = new() { Label = "જન્મ તારીખ" };
		var inquiry = Form(source);
		inquiry.DefaultLanguage = "gu";
		inquiry.SupportedLanguages = ["gu"];
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Definition.Sections.SelectMany(section => section.Fields).Single().Translations["hi"].Label.ShouldBe("જન્મ તારીખ");
	}

	[Fact]
	public void Legacy_layout_overrides_do_not_move_repeated_inquiry_rows()
	{
		var registration = Form(Custom("EDUCATION", "Education", "education"));
		registration.Sections.Single().IsRepeatable = true;
		var inquiry = Form(Custom("SIBLING", "Sibling name", "sibling"));
		inquiry.Sections.Single().IsRepeatable = true;
		var policy = Policy(inquiry.Id);
		policy.Sources.Single().Layouts.Add(new() { FieldIdentity = "custom:sibling", TargetSectionCode = "DETAILS" });
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, policy);
		result.Issues.ShouldBeEmpty();
		result.Definition.Sections.First().Fields.Single().Code.ShouldBe("SIBLING");
		result.Definition.Sections.First().Id.ShouldNotBe(registration.Sections.Single().Id);
	}

	[Fact]
	public void Editable_lookup_cannot_depend_on_readonly_inquiry_only_context()
	{
		var context = Field("SOURCE", "Source", "Inquiry.Source");
		var lookup = Custom("LOOKUP", "Lookup", "lookup");
		lookup.OptionsJson = "{\"source\":\"cities\",\"parentField\":\"SOURCE\"}";
		var inquiry = Form(context, lookup);
		AdmissionFormInheritanceComposer.Compose(Form(), inquiry, Policy(inquiry.Id)).Issues
			.ShouldContain(issue => issue.Code == "ContextDependencyUnsupported");
	}

	[Fact]
	public void Colliding_location_fields_rewrite_country_and_state_validation_references()
	{
		var registration = Form(Custom("COUNTRY", "Other country", "other-country"), Custom("STATE", "Other state", "other-state"));
		var country = Custom("COUNTRY", "Home country", "country");
		var state = Custom("STATE", "Home state", "state");
		state.ValidationJson = "{\"format\":\"state\",\"countryField\":\"COUNTRY\"}";
		var city = Custom("CITY", "Home city", "city");
		city.ValidationJson = "{\"format\":\"city\",\"countryField\":\"COUNTRY\",\"stateField\":\"STATE\"}";
		var inquiry = Form(country, state, city);
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		var fields = result.Definition.Sections.SelectMany(section => section.Fields).ToList();
		using var validation = JsonDocument.Parse(fields.Single(field => field.Label == "Home city").ValidationJson!);
		validation.RootElement.GetProperty("countryField").GetString().ShouldBe(fields.Single(field => field.Label == "Home country").Code);
		validation.RootElement.GetProperty("stateField").GetString().ShouldBe(fields.Single(field => field.Label == "Home state").Code);
	}

	[Fact]
	public void Partial_repeatable_mapping_is_rejected_before_it_splits_source_rows()
	{
		var registration = Form(Custom("NAME", "Sibling name", "sibling-name"));
		var inquiry = Form(Custom("NAME", "Sibling name", "sibling-name"), Custom("AGE", "Sibling age", "sibling-age"));
		registration.Sections[0].IsRepeatable = inquiry.Sections[0].IsRepeatable = true;
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Issues.ShouldContain(issue => issue.Code == "RepeatableGroupConflict");
	}

	[Fact]
	public void Coherent_complete_repeatable_mapping_keeps_one_row_container()
	{
		var registration = Form(Custom("NAME", "Sibling name", "sibling-name"), Custom("AGE", "Sibling age", "sibling-age"));
		var inquiry = Form(Custom("NAME", "Name", "sibling-name"), Custom("AGE", "Age", "sibling-age"));
		registration.Sections[0].IsRepeatable = inquiry.Sections[0].IsRepeatable = true;
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Issues.ShouldBeEmpty();
		var rows = result.Definition.Sections.Where(section => section.Fields.Count > 0).ToList();
		rows.Count.ShouldBe(1);
		rows[0].Fields.Count.ShouldBe(2);
		rows[0].IsRepeatable.ShouldBeTrue();
	}

	[Fact]
	public void Inherited_steps_precede_existing_registration_steps_without_order_collisions()
	{
		var registration = Form(Custom("SCHOOL", "Previous school", "school"));
		registration.LayoutType = AdmissionFormLayoutType.Stepper;
		registration.Sections[0].StepId = Guid.NewGuid();
		registration.Sections[0].StepDisplayOrder = 0;
		var inquiry = Form(Custom("INTEREST", "Learning interests", "interest"));
		inquiry.LayoutType = AdmissionFormLayoutType.Stepper;
		inquiry.Sections[0].StepId = Guid.NewGuid();
		inquiry.Sections[0].StepDisplayOrder = 0;
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Definition.Sections.Single(section => section.Fields.Any(field => field.InheritedFrom != null))
			.StepDisplayOrder.ShouldBe(0);
		result.Definition.Sections.Single(section => section.Fields.All(field => field.InheritedFrom == null))
			.StepDisplayOrder.ShouldBe(1);
	}

	[Fact]
	public void Location_validation_cannot_depend_on_hidden_inquiry_context()
	{
		var context = Field("SOURCE", "Source", "Inquiry.Source");
		var state = Custom("STATE", "State", "state");
		state.ValidationJson = "{\"format\":\"state\",\"countryField\":\"SOURCE\"}";
		var inquiry = Form(context, state);
		AdmissionFormInheritanceComposer.Compose(Form(), inquiry, Policy(inquiry.Id)).Issues
			.ShouldContain(issue => issue.Code == "ContextDependencyUnsupported");
	}
}
