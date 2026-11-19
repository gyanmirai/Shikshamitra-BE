using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquiryIdentityFieldResolver_Tests
{
	[Fact]
	public void Legacy_inquiry_codes_resolve_required_identity_without_label_guessing()
	{
		var formId = Guid.NewGuid();
		var sectionId = Guid.NewGuid();
		var fields = new[]
		{
			Field(formId, sectionId, "STUDENT_NAME", AdmissionFormFieldType.Text),
			Field(formId, sectionId, "DATE_OF_BIRTH", AdmissionFormFieldType.Date),
			Field(formId, sectionId, "GUARDIAN_NAME", AdmissionFormFieldType.Text),
			Field(formId, sectionId, "GUARDIAN_MOBILE", AdmissionFormFieldType.Phone)
		};
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["STUDENT_NAME"] = "raj ",
			["DATE_OF_BIRTH"] = "2016-09-18T00:00:00+05:30",
			["GUARDIAN_NAME"] = "sanjay ",
			["GUARDIAN_MOBILE"] = "8893303939"
		};

		var applicantName = AdmissionInquiryIdentityFieldResolver.Resolve(values, fields,
			"Applicant.Name", null);
		var applicantFirstName = AdmissionInquiryIdentityFieldResolver.Resolve(values, fields,
			"Applicant.FirstName", null) ?? applicantName;

		applicantName.ShouldBe("raj");
		applicantFirstName.ShouldBe("raj");
		AdmissionInquiryIdentityFieldResolver.ResolveDate(values, fields,
			"Applicant.DateOfBirth", null).ShouldBe(new DateTime(2016, 9, 18));
		AdmissionInquiryIdentityFieldResolver.Resolve(values, fields,
			"Guardian.FirstName", null).ShouldBe("sanjay");
		AdmissionInquiryIdentityFieldResolver.Resolve(values, fields,
			"Guardian.Mobile", null).ShouldBe("8893303939");
	}

	[Fact]
	public void Submitted_values_not_defined_by_the_form_are_not_used_as_identity()
	{
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["STUDENT_NAME"] = "Unexpected value"
		};

		AdmissionInquiryIdentityFieldResolver.Resolve(values, [], "Applicant.Name", null).ShouldBeNull();
	}

	[Theory]
	[InlineData("Father.Mobile", "Mother.Mobile")]
	[InlineData("Mother.Mobile", "Father.Mobile")]
	public void Explicit_parent_mobile_binding_does_not_override_selected_parent_fallback_through_legacy_code(
		string otherParentBinding, string selectedParentBinding)
	{
		var formId = Guid.NewGuid();
		var sectionId = Guid.NewGuid();
		var fields = new[]
		{
			Field(formId, sectionId, "PARENT_PHONE", AdmissionFormFieldType.Phone, otherParentBinding),
			Field(formId, sectionId, "SELECTED_PHONE", AdmissionFormFieldType.Phone, selectedParentBinding)
		};
		var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["PARENT_PHONE"] = "9876543210", ["SELECTED_PHONE"] = "9876543211"
		};

		AdmissionInquiryIdentityFieldResolver.Resolve(values, fields, "Guardian.Mobile", "9876543211")
			.ShouldBe("9876543211");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("PARENT_PHONE")]
	[InlineData("Legacy.ParentPhone")]
	[InlineData("Guardian.Mobile")]
	public void Legacy_parent_mobile_code_retains_unbound_unknown_and_matching_binding_compatibility(string? bindingKey)
	{
		var fields = new[] { Field(Guid.NewGuid(), Guid.NewGuid(), "PARENT_PHONE", AdmissionFormFieldType.Phone, bindingKey) };
		var values = new Dictionary<string, string> { ["PARENT_PHONE"] = "9876543210" };

		AdmissionInquiryIdentityFieldResolver.Resolve(values, fields, "Guardian.Mobile", "9876543211")
			.ShouldBe("9876543210");
	}

	private static AdmissionFormFieldDefinition Field(Guid formId, Guid sectionId, string code,
		AdmissionFormFieldType fieldType, string? bindingKey = null) => new(Guid.NewGuid(), formId, sectionId, code, code, fieldType,
		bindingKey == null ? AdmissionFormBindingType.Custom : AdmissionFormBindingType.System,
		bindingKey, true, false, 0, null, null, null, null, null);
}
