using System;
using System.Linq;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionJourneyIdentityConfiguration_Tests
{
	private static readonly string[] DirectCapturedBindings = ["Applicant.FirstName", "Applicant.DateOfBirth", "Applicant.Gender"];

	[Fact]
	public void A_writable_registration_full_name_can_erase_the_structured_inquiry_name()
	{
		AdmissionJourneyIdentityConfiguration.MissingBindings(Form("Applicant.Name"), CompleteForm())
			.ShouldBe(new[] { "Applicant.FirstName", "Applicant.LastName" });
	}

	[Fact]
	public void A_writable_full_name_can_erase_required_registration_parts_regardless_of_saved_answer_order()
	{
		var registration = CompleteForm();
		registration.Sections.AddRange(Form("Applicant.Name").Sections);
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, CompleteForm())
			.ShouldBe(new[] { "Applicant.FirstName", "Applicant.LastName" });
	}

	[Fact]
	public void A_readonly_inherited_full_name_reference_does_not_erase_structured_parts()
	{
		var registration = CompleteForm();
		var context = Form("Applicant.Name").Sections[0];
		context.Fields[0].ReadOnly = true;
		context.Fields[0].InheritedFrom = new() { InquiryFormDefinitionId = Guid.NewGuid(), SourceFieldCode = "SOURCE_NAME", SourceBindingKey = "Applicant.Name" };
		registration.Sections.Add(context);
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, Form("Applicant.Name")).ShouldBeEmpty();
	}

	[Fact]
	public void A_writable_full_name_cannot_rely_on_the_fixed_direct_entry_first_name()
	{
		AdmissionJourneyIdentityConfiguration.MissingBindings(Form("Applicant.Name", "Applicant.LastName"), null, DirectCapturedBindings)
			.ShouldBe(new[] { "Applicant.FirstName", "Applicant.LastName" });
	}

	[Fact]
	public void Fixed_direct_entry_identity_and_a_required_registration_surname_can_complete_the_journey()
	{
		AdmissionJourneyIdentityConfiguration.MissingBindings(Form("Applicant.LastName"), null, DirectCapturedBindings).ShouldBeEmpty();
	}

	[Fact]
	public void Fixed_direct_entry_identity_can_supply_readonly_registration_fields()
	{
		var registration = CompleteForm();
		foreach (var field in registration.Sections.SelectMany(section => section.Fields).Where(field => field.BindingKey != "Applicant.LastName"))
			field.ReadOnly = true;
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null, DirectCapturedBindings).ShouldBeEmpty();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Fixed_direct_entry_does_not_guarantee_an_omitted_or_optional_surname(bool optional)
	{
		var registration = optional ? Form("Applicant.LastName") : Form();
		if (optional) registration.Sections[0].Fields[0].Required = false;
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null, DirectCapturedBindings)
			.ShouldBe(new[] { "Applicant.LastName" });
	}

	[Fact]
	public void A_both_entry_offering_checks_inquiry_without_using_direct_only_capture_guarantees()
	{
		var registration = Form("Applicant.LastName");
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null, DirectCapturedBindings).ShouldBeEmpty();
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, Form("Applicant.Name"))
			.ShouldBe(new[] { "Applicant.FirstName", "Applicant.DateOfBirth", "Applicant.Gender" });
	}

	[Fact]
	public void A_full_name_inquiry_does_not_supply_structured_registration_identity()
	{
		var inquiry = Form("Applicant.Name");
		AdmissionJourneyIdentityConfiguration.MissingBindings(Form(), inquiry)
			.ShouldBe(new[] { "Applicant.FirstName", "Applicant.LastName", "Applicant.DateOfBirth", "Applicant.Gender" });
	}

	[Fact]
	public void Registration_can_collect_identity_without_making_inquiry_collect_it()
	{
		AdmissionJourneyIdentityConfiguration.MissingBindings(CompleteForm(), Form("Applicant.Name")).ShouldBeEmpty();
	}

	[Fact]
	public void A_complete_inquiry_can_supply_identity_omitted_from_registration()
	{
		AdmissionJourneyIdentityConfiguration.MissingBindings(Form(), CompleteForm()).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("optional")]
	[InlineData("readonly")]
	[InlineData("hidden-field")]
	[InlineData("hidden-section")]
	[InlineData("hidden-step")]
	[InlineData("repeated")]
	[InlineData("custom")]
	[InlineData("wrong-type")]
	public void An_unusable_registration_identity_question_does_not_make_the_journey_complete(string mode)
	{
		var registration = CompleteForm();
		MakeUnusable(registration, mode);
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, Form("Applicant.Name"))
			.ShouldBe(new[] { "Applicant.LastName" });
	}

	[Theory]
	[InlineData("optional")]
	[InlineData("readonly")]
	[InlineData("hidden-field")]
	[InlineData("hidden-section")]
	[InlineData("hidden-step")]
	[InlineData("repeated")]
	public void An_untrustworthy_inquiry_prefill_does_not_rescue_an_optional_registration_question(string mode)
	{
		var inquiry = CompleteForm();
		MakeUnusable(inquiry, mode);
		var registration = CompleteForm();
		registration.Sections.SelectMany(section => section.Fields).Single(field => field.BindingKey == "Applicant.LastName").Required = false;
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, inquiry).ShouldBe(new[] { "Applicant.LastName" });
	}

	[Fact]
	public void A_trustworthy_inquiry_prefill_can_supply_a_readonly_registration_field()
	{
		var registration = CompleteForm();
		MakeUnusable(registration, "readonly");
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, CompleteForm()).ShouldBeEmpty();
	}

	[Fact]
	public void A_direct_registration_path_cannot_depend_on_an_inquiry()
	{
		AdmissionJourneyIdentityConfiguration.MissingBindings(Form("Applicant.FirstName"), null)
			.ShouldBe(new[] { "Applicant.LastName", "Applicant.DateOfBirth", "Applicant.Gender" });
	}

	[Fact]
	public void Registration_bindings_must_match_the_case_sensitive_runtime_registry_key()
	{
		var registration = CompleteForm();
		registration.Sections[1].Fields[0].BindingKey = "applicant.lastname";
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null).ShouldBe(new[] { "Applicant.LastName" });
	}

	[Fact]
	public void Constant_true_visibility_and_required_rules_remain_reachable()
	{
		var registration = CompleteForm();
		var field = registration.Sections[1].Fields[0];
		field.Required = false;
		field.RequiredRuleJson = "{\"operator\":\"ALL\",\"conditions\":[]}";
		field.VisibilityRuleJson = field.RequiredRuleJson;
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("[\"Male\",\"Female\"]")]
	[InlineData("[{\"value\":\"Male\"},{\"value\":\"Female\"}]")]
	public void Supported_gender_option_shapes_can_collect_the_identity(string options)
	{
		var registration = CompleteForm();
		registration.Sections[3].Fields[0].OptionsJson = options;
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("[{\"value\":\"boy\"},{\"value\":\"girl\"}]")]
	[InlineData("[{\"value\":\"1\"}]")]
	[InlineData("[]")]
	public void Gender_choices_must_supply_a_supported_identity_value(string options)
	{
		var registration = CompleteForm();
		registration.Sections[3].Fields[0].OptionsJson = options;
		AdmissionJourneyIdentityConfiguration.MissingBindings(registration, null).ShouldBe(new[] { "Applicant.Gender" });
	}

	private static void MakeUnusable(AdmissionFormSchema form, string mode)
	{
		var section = form.Sections.Single(section => section.Fields.Any(field => field.BindingKey == "Applicant.LastName"));
		var field = section.Fields[0];
		switch (mode)
		{
			case "optional": field.Required = false; break;
			case "readonly": field.ReadOnly = true; break;
			case "hidden-field": field.VisibilityRuleJson = "{\"operator\":\"ANY\",\"conditions\":[]}"; break;
			case "hidden-section": section.VisibilityRuleJson = "{\"operator\":\"ANY\",\"conditions\":[]}"; break;
			case "hidden-step": section.StepVisibilityRuleJson = "{\"operator\":\"ANY\",\"conditions\":[]}"; break;
			case "repeated": section.IsRepeatable = true; break;
			case "custom": field.BindingType = AdmissionFormBindingType.Custom; break;
			case "wrong-type": field.FieldType = AdmissionFormFieldType.Checkbox; break;
		}
	}

	private static AdmissionFormSchema CompleteForm() => Form("Applicant.FirstName", "Applicant.LastName", "Applicant.DateOfBirth", "Applicant.Gender");

	private static AdmissionFormSchema Form(params string[] keys) => new()
	{
		Id = Guid.NewGuid(), Sections = keys.Select(key => new AdmissionFormSchemaSection
		{
			Id = Guid.NewGuid(), Fields = [new()
			{
				Code = key, BindingKey = key, BindingType = AdmissionFormBindingType.System, Required = true,
				FieldType = key == "Applicant.DateOfBirth" ? AdmissionFormFieldType.Date : key == "Applicant.Gender" ? AdmissionFormFieldType.Select : AdmissionFormFieldType.Text
			}]
		}).ToList()
	};
}
