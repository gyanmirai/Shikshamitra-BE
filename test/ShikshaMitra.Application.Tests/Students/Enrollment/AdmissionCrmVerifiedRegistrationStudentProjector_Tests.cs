using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

public class AdmissionCrmVerifiedRegistrationStudentProjector_Tests
{
	[Fact]
	public void Should_Project_Verified_Response_Over_Prefill_Into_Student_Record()
	{
		var tenantId = Guid.NewGuid();
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), tenantId, "Prefilled", null, "Applicant",
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "9000000000");
		var now = DateTime.UtcNow;
		var registration = new AdmissionCrmRegistrationApplication(Guid.NewGuid(), tenantId, Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 1, null, "REG-0001", "{}",
			"""{"firstName":"Prefilled","primaryContactNo":"9000000000"}""", now, now.AddDays(30), 0,
			AdmissionCrmRegistrationProvenance.OnlineParent, AdmissionCrmRegistrationAccessMode.Both);
		registration.Submit(
			"""
			{
				"firstName":"Verified",
				"lastName":"Student",
				"dateOfBirth":"2016-05-10",
				"gender":"Female",
				"aadharNo":"123456789012",
				"apparId":"APAAR-123",
				"penNo":"PEN-456",
				"religion":"Hinduism",
				"familyCast":"General",
				"nationality":"Indian",
				"birthPlace":"Surat",
				"motherTongue":"Gujarati",
				"secondLanguage":"Hindi",
				"haveDisability":true,
				"disabilityDetails":"Wheelchair access",
				"primaryContactNo":"9111111111",
				"email":"verified@example.test",
				"localAddress1":"12 School Road",
				"localCity":"Surat",
				"localState":"Gujarat",
				"localCountry":"India",
				"localPincode":"395001",
				"guardianName":"Asha Student",
				"guardianOccupation":"Teacher",
				"previousSchoolName":"Sunrise School",
				"lastClassAttended":"Class 4",
				"bloodGroup":"O+",
				"allergies":"Peanuts",
				"bankName":"School Bank",
				"bankAccountNo":"12345"
			}
			""",
			now,
			false);
		registration.Verify(now);

		new AdmissionCrmVerifiedRegistrationStudentProjector().Project(student, registration);

		student.FirstName.ShouldBe("Verified");
		student.LastName.ShouldBe("Student");
		student.AdditionalDetail!.DateOfBirth.ShouldBe(new DateTimeOffset(2016, 5, 10, 0, 0, 0, TimeSpan.Zero));
		student.AdditionalDetail.Gender.ShouldBe(GenderEnum.Female);
		student.AdditionalDetail.AdharNo.ShouldBe("123456789012");
		student.AdditionalDetail.ApparId.ShouldBe("APAAR-123");
		student.AdditionalDetail.PennNo.ShouldBe("PEN-456");
		student.AdditionalDetail.Nationality.ShouldBe("Indian");
		student.AdditionalDetail.MotherTongue.ShouldBe(MotherTongueEnum.Gujarati);
		student.AdditionalDetail.SecondLanguage.ShouldBe("Hindi");
		student.AdditionalDetail.HaveDisability.ShouldBe(true);
		student.ContactDetail!.PrimaryContactNo.ShouldBe("9111111111");
		student.ContactDetail.Email.ShouldBe("verified@example.test");
		student.FamilyDetail!.GuardianDetails!.FullName.ShouldBe("Asha Student");
		student.CommunicationDetail!.LocalAddress!.City.ShouldBe("Surat");
		student.PreviousSchoolDetail!.SchoolName.ShouldBe("Sunrise School");
		student.MedicalRecord!.BloodGroup.ShouldBe("O+");
		student.BankDetail!.BankName.ShouldBe("School Bank");
	}

	[Fact]
	public void Should_Not_Project_Unverified_Draft()
	{
		var student = Student.CreateDirectRegistration(Guid.NewGuid(), Guid.NewGuid(), "Original", null, "Student",
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			DateTimeOffset.UtcNow.AddYears(-10), GenderEnum.Male, "9000000000");
		var now = DateTime.UtcNow;
		var registration = new AdmissionCrmRegistrationApplication(Guid.NewGuid(), student.TenantId, Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 1, null, "REG-0002", "{}", "{}", now, now.AddDays(30), 0);
		registration.Save("""{"firstName":"Unverified"}""", 10, now);

		new AdmissionCrmVerifiedRegistrationStudentProjector().Project(student, registration);

		student.FirstName.ShouldBe("Original");
	}
}
