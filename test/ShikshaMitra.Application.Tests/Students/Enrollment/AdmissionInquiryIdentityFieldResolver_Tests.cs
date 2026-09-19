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

	private static AdmissionFormFieldDefinition Field(Guid formId, Guid sectionId, string code,
		AdmissionFormFieldType fieldType) => new(Guid.NewGuid(), formId, sectionId, code, code, fieldType,
		AdmissionFormBindingType.Custom, null, true, false, 0, null, null, null, null, null);
}
