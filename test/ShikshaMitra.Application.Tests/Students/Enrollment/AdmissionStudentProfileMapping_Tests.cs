using System;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionStudentProfileMapping_Tests
{
	[Fact]
	public void Accepted_fields_populate_profile_and_preserve_existing_unsupplied_details()
	{
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), null, "Asha", null, "Patel", Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, DateTimeOffset.UtcNow.AddYears(-7),
			GenderEnum.Female, "9876543210");
		student.ApplyVerifiedRegistrationMedical(20, 120, "Existing allergy", "A+", null, null);
		student.MedicalRecord!.UpdateMedicalRecords(20, 120, 13.8m, 75, 12m, "Existing allergy", "Existing vaccination",
			2, "Recorded", "A+", null, null);
		student.ApplyVerifiedRegistrationPreviousSchool("Existing school", null, "CBSE", "English", "1", null, null, null, null);
		student.PreviousSchoolDetail!.UpdatePreviousSchoolDetails("Existing school", null, "CBSE", "English", "1", "INDEX", "TC", null, null, null, null, "Keep remarks");
		var applicant = new AdmissionApplicant(Guid.NewGuid(), null, Guid.NewGuid(), "Asha", null, "Patel", null, null, null, student.Id);
		foreach (var pair in new Dictionary<string, string>
		{
			["Identity.AadhaarNumber"] = "234567890123", ["Identity.PanNumber"] = "ABCDE1234F",
			["Family.IsSingleChild"] = "false", ["Family.IsSingleParentChild"] = "true", ["Applicant.Nationality"] = "Indian",
			["Medical.BloodGroup"] = "O+", ["PreviousSchool.Name"] = "New school", ["Father.Name"] = "Ravi Patel",
			["PermanentAddress.Country"] = "{\"name\":\"India\",\"isoCode\":\"IN\"}"
		}) AdmissionFormBindingRegistry.ApplyValue(pair.Key, pair.Value, applicant, null).ShouldBeTrue();
		AdmissionStudentProfileMapping.Apply(applicant, student);
		student.AdditionalDetail!.AdharNo.ShouldBeNull();
		student.AdditionalDetail.PanNo.ShouldBe("ABCDE1234F");
		student.AdditionalDetail.IsSingleChild.ShouldBe(false);
		student.AdditionalDetail.IsSingleParentChild.ShouldBe(true);
		student.AdditionalDetail.Nationality.ShouldBe("Indian");
		student.FamilyDetail!.FatherDetails!.FullName.ShouldBe("Ravi Patel");
		student.CommunicationDetail!.PermanentAddress!.Country!.Name.ShouldBe("India");
		student.CommunicationDetail.PermanentAddress.Country.IsoCode.ShouldBe("IN");
		student.MedicalRecord.BloodGroup.ShouldBe("O+");
		student.MedicalRecord.Allergies.ShouldBe("Existing allergy");
		student.MedicalRecord.BodyMassIndex.ShouldBe(13.8m);
		student.MedicalRecord.PulseRate.ShouldBe(75);
		student.MedicalRecord.ChildImmunization.ShouldBe("Existing vaccination");
		student.PreviousSchoolDetail.SchoolName.ShouldBe("New school");
		student.PreviousSchoolDetail.TCNumber.ShouldBe("TC");
		student.PreviousSchoolDetail.Remarks.ShouldBe("Keep remarks");
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Mapping_preserves_unsupplied_emergency_address_and_same_flags(bool supplyPermanentCountry)
	{
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), null, "Asha", null, "Patel", Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddYears(-6), GenderEnum.Female, "9876543210");
		var emergency = new ShikshaMitra.Students.ValueObjects.AddressDetails("Emergency address", null, null, "Ahmedabad", null, null, "380001");
		student.ApplyVerifiedRegistrationCommunication(null, null, false, "Existing guardian", "9876543210", "Aunt", emergency, false);
		var applicant = new AdmissionApplicant(Guid.NewGuid(), null, Guid.NewGuid(), "Asha", null, "Patel", null, null, null, student.Id);
		AdmissionFormBindingRegistry.ApplyValue(supplyPermanentCountry ? "PermanentAddress.Country" : "Medical.BloodGroup",
			supplyPermanentCountry ? "India" : "O+", applicant, null);
		AdmissionStudentProfileMapping.Apply(applicant, student);
		student.CommunicationDetail!.IsPermanentSameAsLocal.ShouldBeFalse();
		student.CommunicationDetail.IsEmergencySameAsLocal.ShouldBeFalse();
		student.CommunicationDetail.EmergencyAddress!.Address1.ShouldBe("Emergency address");
		student.CommunicationDetail.EmergencyContactName.ShouldBe("Existing guardian");
	}

	[Theory]
	[InlineData("data:image/png;base64,bm90IGFuIGltYWdl")]
	[InlineData("data:image/svg+xml;base64,PHN2Zy8+")]
	[InlineData("https://example.com/photo.png")]
	[InlineData("data:image/jpeg;base64,invalid")]
	public void Photo_rejects_invalid_content_and_external_urls(string value) =>
		Should.Throw<BusinessException>(() => AdmissionStudentPhoto.Decode(value));

	[Fact]
	public void Photo_rejects_oversized_payload_before_decoding() => Should.Throw<BusinessException>(() =>
		AdmissionStudentPhoto.Decode("data:image/png;base64," + new string('A', 3 * 1024 * 1024)));

	[Fact]
	public void Sibling_selection_rejects_non_identifiers_and_empty_id()
	{
		Should.Throw<BusinessException>(() => AdmissionStudentProfileMapping.ReadSiblings("[\"student-name\"]"));
		Should.Throw<BusinessException>(() => AdmissionStudentProfileMapping.ReadSiblings(JsonSerializer.Serialize(new[] { Guid.Empty })));
	}
}
