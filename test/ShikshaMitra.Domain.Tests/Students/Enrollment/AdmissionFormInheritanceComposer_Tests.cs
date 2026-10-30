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
			.ShouldBe(new[] { "Previous school", "Transport required?" });
		b.Definition.Sections.SelectMany(section => section.Fields).Select(field => field.Label)
			.ShouldBe(new[] { "Previous school", "Previous class marks" });
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
		dependentResult.VisibilityRuleJson.ShouldContain(inheritedCode);
		dependentResult.OptionsJson.ShouldContain(inheritedCode);
	}

	[Fact]
	public void Registration_can_strengthen_requiredness_but_cannot_create_a_blank_readonly_requirement()
	{
		var source = Field("DOB", "DOB", "Applicant.DateOfBirth");
		var inquiry = Form(source);
		var policy = Policy(inquiry.Id);
		policy.Sources.Single().RequiredFieldIdentities.Add("system:APPLICANT.DATEOFBIRTH");
		var result = AdmissionFormInheritanceComposer.Compose(Form(), inquiry, policy);
		result.Definition.Sections.SelectMany(section => section.Fields).Single().Required.ShouldBeTrue();

		source.ReadOnly = true;
		AdmissionFormInheritanceComposer.Compose(Form(), inquiry, policy).Issues
			.ShouldContain(issue => issue.Code == "ReadonlyRequiredOverride");
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
		result.Definition.Sections.SelectMany(section => section.Fields).Single().OptionsJson.ShouldContain("Self");
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
	public void Repeated_rows_cannot_be_moved_into_a_different_repeated_group()
	{
		var registration = Form(Custom("EDUCATION", "Education", "education"));
		registration.Sections.Single().IsRepeatable = true;
		var inquiry = Form(Custom("SIBLING", "Sibling name", "sibling"));
		inquiry.Sections.Single().IsRepeatable = true;
		var policy = Policy(inquiry.Id);
		policy.Sources.Single().Layouts.Add(new() { FieldIdentity = "custom:sibling", TargetSectionCode = "DETAILS" });
		AdmissionFormInheritanceComposer.Compose(registration, inquiry, policy).Issues.ShouldContain(issue => issue.Code == "InvalidLayout");
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
	public void Inherited_steps_follow_existing_registration_steps_without_order_collisions()
	{
		var registration = Form(Custom("SCHOOL", "Previous school", "school"));
		registration.Sections[0].StepId = Guid.NewGuid();
		registration.Sections[0].StepDisplayOrder = 0;
		var inquiry = Form(Custom("INTEREST", "Learning interests", "interest"));
		inquiry.Sections[0].StepId = Guid.NewGuid();
		inquiry.Sections[0].StepDisplayOrder = 0;
		var result = AdmissionFormInheritanceComposer.Compose(registration, inquiry, Policy(inquiry.Id));
		result.Definition.Sections.Single(section => section.Fields.Any(field => field.InheritedFrom != null))
			.StepDisplayOrder.ShouldNotBeNull().ShouldBeGreaterThan(0);
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
