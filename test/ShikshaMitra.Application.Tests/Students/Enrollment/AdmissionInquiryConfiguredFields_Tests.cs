using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryConfiguredFields_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Legacy_edit_preserves_saved_identity_for_omitted_optional_mobile_but_not_an_explicit_blank(bool explicitBlank)
	{
		var fields = new[] { Field("MOBILE", "Guardian.Mobile", AdmissionFormFieldType.Phone, false), Field("OWN_EMAIL", "Father.Email", AdmissionFormFieldType.Email, false), Field("EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false) };
		var answers = new Dictionary<string, string> { ["EMAIL"] = "changed@example.com" };
		if (explicitBlank) answers["MOBILE"] = "";
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, answers, "father", allowLegacySplitContact: true);
		contact.Relationship.ShouldBe("father");
		contact.NameIsConfigured.ShouldBeFalse();
		contact.MobileIsConfigured.ShouldBe(explicitBlank);
		contact.IsConfigured.ShouldBe(explicitBlank);
		contact.Email.ShouldBe("changed@example.com");
	}
	[Fact]
	public void Section_hidden_email_cannot_own_contact_but_saved_readonly_email_remains_authoritative_on_edit()
	{
		var fields = new[] { Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select), Field("EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false), Field("FATHER_EMAIL", "Father.Email", AdmissionFormFieldType.Email, false, readOnly: true) };
		var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ADULT", "PHONE", "ROLE", "EMAIL" };
		var answers = new Dictionary<string, string> { ["ADULT"] = "Father", ["PHONE"] = "9876543210", ["ROLE"] = "father", ["EMAIL"] = "visible@example.com", ["FATHER_EMAIL"] = "saved-readonly@example.com" };
		AdmissionInquiryConfiguredFields.ResolveContact(fields, answers, activeFieldCodes: active).Email.ShouldBe("visible@example.com");
		AdmissionInquiryConfiguredFields.ResolveContact(fields, answers, "father", allowLegacySplitContact: true, activeFieldCodes: active).Email.ShouldBe("saved-readonly@example.com");
	}

	[Theory]
	[InlineData("mother", false, "Saved mother", "9876543211")]
	[InlineData("father", true, "Saved generic", "9876543212")]
	public void Optional_generic_resolution_preserves_saved_roles_and_locked_generic_identity(string savedRole, bool locked, string expectedName, string expectedPhone)
	{
		var fields = new[] { Field("FATHER", "Father.Name"), Field("FATHER_PHONE", "Father.Mobile", AdmissionFormFieldType.Phone), Field("MOTHER", "Mother.Name", required: false), Field("MOTHER_PHONE", "Mother.Mobile", AdmissionFormFieldType.Phone, false), Field("GENERIC", "Guardian.FirstName", required: false), Field("GENERIC_PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone, false, readOnly: locked) };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["FATHER"] = "Required father", ["FATHER_PHONE"] = "9876543210", ["MOTHER"] = "Saved mother", ["MOTHER_PHONE"] = "9876543211", ["GENERIC"] = "Saved generic", ["GENERIC_PHONE"] = "9876543212" }, savedRole, allowLegacySplitContact: true);
		contact.Relationship.ShouldBe(savedRole);
		contact.Name.ShouldBe(expectedName);
		contact.Mobile.ShouldBe(expectedPhone);
	}
	[Fact]
	public void Hidden_selected_parent_email_cannot_claim_the_visible_generic_primary_email()
	{
		var fields = new[] { Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select), Field("EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false), Field("FATHER_EMAIL", "Father.Email", AdmissionFormFieldType.Email, false, rule: "{\"field\":\"ROLE\",\"operator\":\"notEquals\",\"value\":\"father\"}") };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["ADULT"] = "Father", ["PHONE"] = "9876543210", ["ROLE"] = "father", ["EMAIL"] = "father@example.com" });
		contact.Relationship.ShouldBe("father");
		contact.Email.ShouldBe("father@example.com");
		contact.EmailIsConfigured.ShouldBeTrue();
	}
	[Theory]
	[InlineData("Father", "father")]
	[InlineData("Mother", "mother")]
	public void Legacy_required_parent_identity_owns_contact_when_generic_pair_is_optional_without_selector(string prefix, string role)
	{
		var fields = new[] { Field("ADULT", prefix + ".Name"), Field("PHONE", prefix + ".Mobile", AdmissionFormFieldType.Phone), Field("EMAIL", prefix + ".Email", AdmissionFormFieldType.Email, false), Field("GENERIC", "Guardian.FirstName", required: false), Field("GENERIC_PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone, false), Field("GENERIC_EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false) };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["ADULT"] = "Required parent", ["PHONE"] = "9876543210", ["EMAIL"] = "parent@example.com", ["GENERIC"] = "", ["GENERIC_PHONE"] = "", ["GENERIC_EMAIL"] = "" });
		contact.Relationship.ShouldBe(role);
		contact.Name.ShouldBe("Required parent");
		contact.Mobile.ShouldBe("9876543210");
		contact.Email.ShouldBe("parent@example.com");
	}
	[Theory]
	[InlineData(null, true)]
	[InlineData("{\"field\":\"ROLE\",\"operator\":\"equals\",\"value\":\"mother\"}", false)]
	public void Publication_keeps_other_parent_email_separate_from_primary_generic_email(string? fatherEmailVisibility, bool conflict)
	{
		var fields = new[] { Field("CHILD", "Applicant.Name"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select), Field("EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false), Field("DAD_EMAIL", "Father.Email", AdmissionFormFieldType.Email, false, rule: fatherEmailVisibility) };
		var issues = AdmissionInquiryConfiguredFields.Validate(fields, [Section()]);
		if (conflict) issues.ShouldContain(AdmissionInquiryConfiguredFields.ConflictingEmail);
		else issues.ShouldBeEmpty();
	}
	[Theory]
	[InlineData("father", "father@example.com")]
	[InlineData("mother", "mother@example.com")]
	[InlineData("guardian", "adult@example.com")]
	public void Required_generic_identity_maps_to_the_configured_primary_role_with_its_own_email(string role, string expectedEmail)
	{
		var fields = new[] { Field("CHILD", "Applicant.Name"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("EMAIL", "Guardian.Email", AdmissionFormFieldType.Email, false), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select), Field("DAD", "Father.Name", required: false), Field("DP", "Father.Mobile", AdmissionFormFieldType.Phone, false), Field("DE", "Father.Email", AdmissionFormFieldType.Email, false), Field("MUM", "Mother.Name", required: false), Field("MP", "Mother.Mobile", AdmissionFormFieldType.Phone, false), Field("ME", "Mother.Email", AdmissionFormFieldType.Email, false) };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["ADULT"] = "Selected adult", ["PHONE"] = "9876543210", ["ROLE"] = role, ["EMAIL"] = "adult@example.com", ["DE"] = "father@example.com", ["ME"] = "mother@example.com" });
		contact.Relationship.ShouldBe(role);
		contact.Name.ShouldBe("Selected adult");
		contact.Mobile.ShouldBe("9876543210");
		contact.Email.ShouldBe(expectedEmail);
	}

	[Fact]
	public void Guardian_is_a_primary_contact_choice_but_never_a_sibling_parent_choice()
	{
		var fields = new[] { Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("ROLE", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select) };
		AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["ADULT"] = "Aunt", ["PHONE"] = "9876543210", ["ROLE"] = "guardian" }).Relationship.ShouldBe("guardian");
		AdmissionSiblingParentSelection.IsRelationship("guardian").ShouldBeFalse();
	}
	[Theory]
	[InlineData("Father.Name", AdmissionFormFieldType.Text)]
	[InlineData("Mother.Mobile", AdmissionFormFieldType.Phone)]
	public void Publication_accepts_optional_parent_extras_when_required_generic_contact_owns_identity(string binding, AdmissionFormFieldType type)
	{
		var fields = new[] { Field("CHILD", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone), Field("PRIMARY", "Inquiry.ParentRelationship", AdmissionFormFieldType.Select), Field("PARTIAL", binding, type) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldBeEmpty();
	}

	[Fact]
	public void Publication_rejects_custom_aliases_that_could_shadow_explicit_identity()
	{
		var legacy = new AdmissionFormFieldDefinition(Guid.NewGuid(), _formId, _sectionId, "APPLICANT_FIRST_NAME", "Custom answer", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, "CUSTOM_ANSWER", true, false, 0, null, null, null, null, null);
		var fields = new[] { legacy, Field("CHILD", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone) };
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldNotBeEmpty();
	}

	[Fact]
	public void Publication_requires_an_explicit_mapping_while_runtime_keeps_legacy_aliases()
	{
		var legacy = new AdmissionFormFieldDefinition(Guid.NewGuid(), _formId, _sectionId, "APPLICANT_FIRST_NAME", "Full Name", AdmissionFormFieldType.Text, AdmissionFormBindingType.Custom, "FIELD_1", true, false, 0, null, null, null, null, null);
		var fields = new[] { legacy, Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone) };
		AdmissionInquiryConfiguredFields.Read(fields, new Dictionary<string, string> { [legacy.Code] = "Riya" }, "Applicant.FirstName").ShouldBe("Riya");
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain(AdmissionInquiryConfiguredFields.MissingStudent);
	}

	[Theory]
	[InlineData("Applicant.FirstName", AdmissionInquiryConfiguredFields.MissingStudent)]
	[InlineData("Guardian.FirstName", AdmissionInquiryConfiguredFields.MissingParent)]
	[InlineData("Guardian.Mobile", AdmissionInquiryConfiguredFields.MissingMobile)]
	public void Publication_rejects_custom_questions_with_system_looking_keys(string key, string issue)
	{
		var fields = new[] { Field("CHILD", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone) };
		fields = fields.Select(field => field.BindingKey != key ? field : new AdmissionFormFieldDefinition(field.Id, _formId, _sectionId, field.Code, field.Label, field.FieldType, AdmissionFormBindingType.Custom, key, true, false, 0, null, null, null, null, null)).ToArray();
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain(issue);
	}

	[Theory]
	[InlineData("Applicant.FirstName", AdmissionInquiryConfiguredFields.MissingStudent)]
	[InlineData("Guardian.FirstName", AdmissionInquiryConfiguredFields.MissingParent)]
	[InlineData("Guardian.Mobile", AdmissionInquiryConfiguredFields.MissingMobile)]
	public void Publication_rejects_identity_questions_with_incompatible_types(string key, string issue)
	{
		var fields = new[] { Field("CHILD", "Applicant.FirstName"), Field("ADULT", "Guardian.FirstName"), Field("PHONE", "Guardian.Mobile", AdmissionFormFieldType.Phone) };
		fields = fields.Select(field => field.BindingKey != key ? field : Field(field.Code, key, AdmissionFormFieldType.Number)).ToArray();
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain(issue);
	}

	[Theory]
	[InlineData("Father", "father")]
	[InlineData("Mother", "mother")]
	public void Partial_explicit_parent_mobile_is_retained_without_inventing_the_missing_name(string prefix, string role)
	{
		var fields = new[] { Field("STUDENT", "Applicant.Name"), Field("PHONE", prefix + ".Mobile", AdmissionFormFieldType.Phone) };
		var contact = AdmissionInquiryConfiguredFields.ResolveContact(fields, new Dictionary<string, string> { ["STUDENT"] = "Riya", ["PHONE"] = "9876543210" });
		contact.Relationship.ShouldBe(role);
		contact.Mobile.ShouldBe("9876543210");
		contact.Name.ShouldBeNull();
		contact.IsConfigured.ShouldBeTrue();
		contact.NameIsConfigured.ShouldBeFalse();
		contact.MobileIsConfigured.ShouldBeTrue();
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldNotBeEmpty();
	}

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
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain("Add a required Primary contact selector when both Father and Mother contacts are configured.");
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
		AdmissionInquiryConfiguredFields.Validate(fields, [Section()]).ShouldContain("Configure a required Primary contact selector with Father, Mother, and Guardian choices before publishing.");
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
		var unselected = AdmissionInquiryConfiguredFields.ResolveContact(fields, values);
		unselected.IsConfigured.ShouldBeFalse();
		unselected.Mobile.ShouldBeNull(); // No selected owner: never borrow either parent's number.
		unselected.Relationship.ShouldBeNull();
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
		new(Guid.NewGuid(), _formId, _sectionId, code, "Custom " + code, type, AdmissionFormBindingType.System, binding, required, readOnly, 0, null, null, null, null, rule, binding == "Inquiry.ParentRelationship" ? "[{\"value\":\"father\",\"label\":\"Father\"},{\"value\":\"mother\",\"label\":\"Mother\"}]" : null);
}
