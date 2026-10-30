using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryConfiguredFields_Tests
{
	private readonly Guid _formId = Guid.NewGuid();
	private readonly Guid _sectionId = Guid.NewGuid();

	[Theory]
	[InlineData("Father", "father")]
	[InlineData("Mother", "mother")]
	public void A_single_configured_parent_supplies_its_own_contact_without_generic_questions(string prefix, string role)
	{
		var fields = new[] { Field("STUDENT", "Applicant.Name"), Field("ADULT", prefix + ".Name"), Field("PHONE", prefix + ".Mobile", AdmissionFormFieldType.Phone), Field("EMAIL", prefix + ".Email", AdmissionFormFieldType.Email, false) };
		var values = new Dictionary<string, string> { ["STUDENT"] = "Riya Shah", ["ADULT"] = "Amit Shah", ["PHONE"] = "9876543210", ["EMAIL"] = "amit@example.com" };

		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldBeEmpty();
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, values);
		contact.Relationship.ShouldBe(role);
		contact.Name.ShouldBe("Amit Shah");
		contact.Mobile.ShouldBe("9876543210");
		contact.Email.ShouldBe("amit@example.com");
	}

	[Fact]
	public void Both_parents_require_a_configured_selector_and_never_mix_people()
	{
		var fields = new[] { Field("STUDENT", "Applicant.FirstName"), Field("DAD", "Father.Name"), Field("DAD_PHONE", "Father.Mobile", AdmissionFormFieldType.Phone), Field("MUM", "Mother.Name"), Field("MUM_PHONE", "Mother.Mobile", AdmissionFormFieldType.Phone), Field("DAD_EMAIL", "Father.Email", AdmissionFormFieldType.Email, false) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain("Add a required Primary parent selector when both Father and Mother contacts are configured.");
		fields = [.. fields, Field("PRIMARY", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select)];
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldBeEmpty();
		var values = new Dictionary<string, string> { ["DAD"] = "Amit", ["DAD_PHONE"] = "9876543210", ["DAD_EMAIL"] = "amit@example.com", ["MUM"] = "Neha", ["MUM_PHONE"] = "9876543211", ["PRIMARY"] = "mother" };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, values);
		contact.Relationship.ShouldBe("mother");
		contact.Name.ShouldBe("Neha");
		contact.Mobile.ShouldBe("9876543211");
		contact.Email.ShouldBeNull();
	}

	[Fact]
	public void Generic_primary_contact_is_valid_without_inventing_a_parent_role()
	{
		var fields = new[] { Field("STUDENT", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldBeEmpty();
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["ADULT"] = "An adult", ["PHONE"] = "9876543210" });
		contact.Relationship.ShouldBeNull();
		contact.Name.ShouldBe("An adult");
		contact.Mobile.ShouldBe("9876543210");
	}

	[Theory]
	[InlineData(false, false, null)]
	[InlineData(true, true, null)]
	[InlineData(true, false, "{\"field\":\"SHOW\",\"operator\":\"equals\",\"value\":\"yes\"}")]
	[InlineData(true, false, "{}")]
	[InlineData(true, false, "null")]
	public void Optional_locked_or_conditional_mobile_cannot_be_published_as_minimum_contact(bool required, bool readOnly, string? rule)
	{
		var fields = new[] { Field("STUDENT", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone, required, readOnly, rule) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain("Add a required Parent / Guardian mobile field before publishing.");
	}

	[Fact]
	public void Hidden_section_does_not_supply_minimum_identity()
	{
		var fields = new[] { Field("STUDENT", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section("{\"field\":\"SHOW\",\"operator\":\"equals\",\"value\":\"yes\"}")]).Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Registered_opposite_parent_binding_cannot_be_reinterpreted_by_a_generic_legacy_code()
	{
		var field = Field("PARENT_PHONE", "Mother.Mobile", AdmissionFormFieldType.Phone);
		AdmissionInquiryConfiguredFields.Find([field], "Guardian.Mobile").ShouldBeNull();
		AdmissionInquiryConfiguredFields.Find([Field("PARENT_PHONE", "Legacy.ParentPhone", AdmissionFormFieldType.Phone)], "Guardian.Mobile").ShouldNotBeNull();
	}

	[Fact]
	public void Explicit_selector_can_describe_a_generic_primary_contact()
	{
		var fields = new[] { Field("CHILD", "Applicant.Name"), Field("NAME", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldBeEmpty();
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["NAME"] = "Neha", ["PHONE"] = "9876543210", ["ROLE"] = "mother" });
		contact.Relationship.ShouldBe("mother");
		contact.Name.ShouldBe("Neha");
	}

	[Fact]
	public void Configured_selector_without_a_matching_contact_path_cannot_be_published()
	{
		var fields = new[] { Field("CHILD", "Applicant.Name"), Field("NAME", "Father.Name"), Field("PHONE", "Father.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain("Configure a required Primary parent selector with Father and Mother choices before publishing.");
	}

	[Fact]
	public void Existing_saved_role_can_resolve_legacy_generic_name_and_its_own_structured_mobile_on_edit()
	{
		var fields = new[] { Field("NAME", "Guardian.FirstName"), Field("MUM_PHONE", "Mother.Mobile", AdmissionFormFieldType.Phone), Field("DAD_PHONE", "Father.Mobile", AdmissionFormFieldType.Phone) };
		var values = new Dictionary<string, string> { ["NAME"] = "Neha", ["MUM_PHONE"] = "9876543211", ["DAD_PHONE"] = "9876543210" };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, values, "mother", allowLegacySplitContact: true);
		contact.Relationship.ShouldBe("mother");
		contact.Name.ShouldBe("Neha");
		contact.Mobile.ShouldBe("9876543211");
		AdmissionInquiryConfiguredFields.ResolveContact(fields, values).IsConfigured.ShouldBeFalse();
	}

	[Fact]
	public void Public_operational_answers_do_not_replace_link_source_attribution()
	{
		var fields = new[] { Field("SOURCE", "Inquiry.Source", AdmissionFormFieldType.Select, false), Field("DETAIL", "Inquiry.SourceDetail", required: false) };
		var input = new CreateAdmissionInquiryDto { Source = AdmissionInquirySource.QrCode, SourceDetail = "School QR" };
		AdmissionInquiryConfiguredFields.ApplyOperationalValues(fields, new Dictionary<string, string> { ["SOURCE"] = "8", ["DETAIL"] = "Spoofed" }, input, true);
		input.Source.ShouldBe(AdmissionInquirySource.QrCode);
		input.SourceDetail.ShouldBe("School QR");
	}

	[Fact]
	public void Inquiry_validation_skips_hidden_steps_without_changing_registration_validation()
	{
		var section = new AdmissionFormSectionDefinition(_sectionId, _formId, "OPTIONAL", "Optional", 0, null,
			stepVisibilityRuleJson: "{\"field\":\"SHOW\",\"operator\":\"equals\",\"value\":\"yes\"}");
		var fields = new[] { Field("DETAIL", "Custom.Detail") };
		var validator = new AdmissionApplicationFormValidator(new AdmissionWorkflowConditionEvaluator());
		validator.ValidateInquiry(fields, [section], new Dictionary<string, string> { ["SHOW"] = "no" }).FieldErrors.ShouldBeEmpty();
		validator.ValidateInquiry(fields, [section], new Dictionary<string, string> { ["SHOW"] = "yes" }).FieldErrors.ShouldContainKey("DETAIL");
		validator.Validate(fields, [section], new Dictionary<string, string> { ["SHOW"] = "no" }).FieldErrors.ShouldContainKey("DETAIL");
	}

	[Fact]
	public void Registered_address_binding_does_not_become_student_or_parent_identity_through_code()
	{
		AdmissionInquiryConfiguredFields.Find([Field("FIRST_NAME", "Address.City")], "Applicant.FirstName").ShouldBeNull();
		AdmissionInquiryConfiguredFields.Find([Field("PARENT_PHONE", "Address.City")], "Guardian.Mobile").ShouldBeNull();
	}

	[Fact]
	public void Selector_uses_required_structured_paths_without_requiring_optional_generic_questions()
	{
		var fields = new[] { Field("CHILD", "Applicant.Name"), Field("DAD", "Father.Name"), Field("DP", "Father.Mobile", AdmissionFormFieldType.Phone), Field("MUM", "Mother.Name"), Field("MP", "Mother.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select), Field("GENERIC", "Guardian.FirstName", required: false), Field("GENERIC_PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone, false) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldBeEmpty();
	}

	[Theory]
	[InlineData("")]
	[InlineData("0")]
	public void Optional_source_blank_retains_default_and_explicit_zero_is_valid(string answer)
	{
		var fields = new[] { Field("SOURCE", "Inquiry.Source", AdmissionFormFieldType.Select, false) };
		var input = new CreateAdmissionInquiryDto { Source = AdmissionInquirySource.Website };
		AdmissionInquiryConfiguredFields.ApplyOperationalValues(fields, new Dictionary<string, string> { ["SOURCE"] = answer }, input, false);
		input.Source.ShouldBe(AdmissionInquirySource.Website);
	}

	[Fact]
	public void Omitted_source_detail_preserves_existing_value_but_explicit_blank_can_clear()
	{
		var fields = new[] { Field("DETAIL", "Inquiry.SourceDetail", required: false) };
		var input = new CreateAdmissionInquiryDto { SourceDetail = "Saved detail" };
		AdmissionInquiryConfiguredFields.ApplyOperationalValues(fields, new Dictionary<string, string>(), input, false);
		input.SourceDetail.ShouldBe("Saved detail");
		AdmissionInquiryConfiguredFields.ApplyOperationalValues(fields, new Dictionary<string, string> { ["DETAIL"] = "" }, input, false);
		input.SourceDetail.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("Riya", "Surya", "Shah", "Riya Surya Shah")]
	[InlineData("", "Unused", "Surname", "Full Name")]
	public void Legacy_mixed_names_have_deterministic_nonblank_split_or_full_fallback(string first, string middle, string last, string expected)
	{
		var fields = new[] { Field("FULL", "Applicant.Name"), Field("FIRST", "Applicant.FirstName", required: false), Field("MIDDLE", "Applicant.MiddleName", required: false), Field("LAST", "Applicant.LastName", required: false) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain("Use either Student name or separate student name fields before publishing.");
		var applicant = new AdmissionApplicant(Guid.NewGuid(), null, Guid.NewGuid(), "Old", null, "Existing", null, null, null, Guid.NewGuid());
		AdmissionInquiryConfiguredFields.ApplyApplicantName(applicant, fields, new Dictionary<string, string> { ["FULL"] = "Full Name", ["FIRST"] = first, ["MIDDLE"] = middle, ["LAST"] = last });
		string.Join(" ", new[] { applicant.FirstName, applicant.MiddleName, applicant.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))).ShouldBe(expected);
	}

	[Fact]
	public void Generic_email_question_belongs_to_selected_primary_only_when_own_email_question_is_absent()
	{
		var fields = new[] { Field("DAD", "Father.Name"), Field("PHONE", "Father.Mobile", AdmissionFormFieldType.Phone), Field("GENERIC_EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false) };
		var answers = new Dictionary<string, string> { ["DAD"] = "Amit", ["PHONE"] = "9876543210", ["GENERIC_EMAIL"] = "amit@example.com", ["OWN_EMAIL"] = "" };
		AdmissionInquiryConfiguredFields.ResolveContact(fields, answers).Email.ShouldBe("amit@example.com");
		AdmissionInquiryConfiguredFields.ResolveContact([.. fields, Field("OWN_EMAIL", "Father.Email", AdmissionFormFieldType.Email, false)], answers).Email.ShouldBeEmpty();
	}

	[Fact]
	public void Configured_blank_parent_mobile_stays_blank_instead_of_borrowing_unconfigured_request_identity()
	{
		var fields = new[] { Field("DAD", "Father.Name"), Field("PHONE", "Father.Mobile", AdmissionFormFieldType.Phone) };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["DAD"] = "Amit", ["PHONE"] = "" });
		contact.Mobile.ShouldBeNullOrEmpty();
		contact.Relationship.ShouldBe("father");
	}

	private AdmissionFormSectionDefinition Section(string? rule = null) => new(_sectionId, _formId, "DETAILS", "Details", 0, rule);
	private AdmissionFormFieldDefinition Field(string code, string binding, AdmissionFormFieldType type = AdmissionFormFieldType.Text, bool required = true, bool readOnly = false, string? rule = null) =>
		new(Guid.NewGuid(), _formId, _sectionId, code, "Custom " + code, type, AdmissionFormBindingType.System, binding, required, readOnly, 0, null, null, null, null, rule);
}
