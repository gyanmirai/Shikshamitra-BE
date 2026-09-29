using System;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionAdditionalStudentFieldCatalog_Tests
{
	[Fact]
	public void Catalogue_contains_one_hundred_unique_student_fields_without_financial_assistance()
	{
		AdmissionFormBindingRegistry.Metadata.Count.ShouldBe(100);
		AdmissionAdditionalStudentFieldCatalog.Metadata.Count.ShouldBe(85);
		AdmissionFormBindingRegistry.Metadata.Keys.ShouldAllBe(key =>
			!key.StartsWith("FinancialAssistance.", StringComparison.OrdinalIgnoreCase));
		foreach (var field in AdmissionFormBindingRegistry.Metadata.Values.Where(field =>
			field.FieldType == AdmissionFormFieldType.Select))
		{
			field.OptionsJson.ShouldNotBeNullOrWhiteSpace();
			using var options = JsonDocument.Parse(field.OptionsJson);
			options.RootElement.GetArrayLength().ShouldBeGreaterThan(0);
		}
	}

	[Fact]
	public void Additional_values_prefill_registration_and_follow_applicant_into_student_record()
	{
		var applicant = new AdmissionApplicant(Guid.NewGuid(), null, Guid.NewGuid(), "Asha", null,
			"Patel", null, null, null, Guid.NewGuid());
		var student = new Student(Guid.NewGuid(), null, "Asha", null, "Patel", Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

		AdmissionFormBindingRegistry.ApplyValue("medical.bloodgroup", "O+", applicant, null).ShouldBeTrue();
		AdmissionFormBindingRegistry.ApplyValue("Identity.PenNumber", "PEN-123", applicant, null).ShouldBeTrue();
		AdmissionFormBindingRegistry.ApplyValue("Eligibility.IsBPL", "true", applicant, null).ShouldBeTrue();
		AdmissionFormBindingRegistry.ApplyValue("Medical.HeightCm", "132.5", applicant, null).ShouldBeTrue();
		AdmissionFormBindingRegistry.ApplyValue("Unknown.Field", "ignored", applicant, null).ShouldBeFalse();

		AdmissionFormBindingRegistry.GetValue("Medical.BloodGroup", applicant, null).ShouldBe("O+");
		AdmissionFormBindingRegistry.GetValue("Eligibility.IsBPL", applicant, null).ShouldBe(true);
		AdmissionFormBindingRegistry.GetValue("Medical.HeightCm", applicant, null).ShouldBe(132.5m);

		AdmissionAdditionalStudentFieldCatalog.CopyToStudent(applicant, student);
		student.ExtraProperties["AdmissionStudentField.Medical.BloodGroup"].ShouldBe("O+");
		student.ExtraProperties["AdmissionStudentField.Identity.PenNumber"].ShouldBe("PEN-123");
		student.ExtraProperties.ContainsKey("AdmissionStudentField.Unknown.Field").ShouldBeFalse();

		AdmissionFormBindingRegistry.ApplyValue("Identity.PenNumber", "", applicant, null).ShouldBeTrue();
		AdmissionFormBindingRegistry.GetValue("Identity.PenNumber", applicant, null).ShouldBeNull();
	}
}
