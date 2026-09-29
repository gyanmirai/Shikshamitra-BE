using System;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students;

public class AdmissionApplicant_Tests
{
	[Fact]
	public void Applicant_requires_a_nonempty_reserved_Student_identity()
	{
		Should.Throw<ArgumentException>(() => CreateApplicant(Guid.Empty));
	}

	[Fact]
	public void Reservation_survives_profile_edits_without_linking_a_Student()
	{
		var reservedStudentId = Guid.NewGuid();
		var applicant = CreateApplicant(reservedStudentId);

		applicant.UpdateProfile(" Updated ", "Middle", "Name", new DateTime(2018, 2, 3), "Female",
			"New address", city: "Pune", postalCode: "411001");

		applicant.FirstName.ShouldBe("Updated");
		applicant.City.ShouldBe("Pune");
		applicant.ReservedStudentId.ShouldBe(reservedStudentId);
		applicant.ExistingStudentId.ShouldBeNull();
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Linking_new_or_existing_Student_preserves_the_reservation(bool useReservation)
	{
		var reservedStudentId = Guid.NewGuid();
		var applicant = CreateApplicant(reservedStudentId);
		var actualStudentId = useReservation ? reservedStudentId : Guid.NewGuid();

		applicant.LinkStudent(actualStudentId);
		applicant.LinkStudent(actualStudentId);

		applicant.ExistingStudentId.ShouldBe(actualStudentId);
		applicant.ReservedStudentId.ShouldBe(reservedStudentId);
	}

	[Fact]
	public void Existing_Student_link_cannot_be_replaced_with_the_unused_reservation()
	{
		var reservedStudentId = Guid.NewGuid();
		var actualStudentId = Guid.NewGuid();
		var applicant = CreateApplicant(reservedStudentId, actualStudentId);

		Should.Throw<BusinessException>(() => applicant.LinkStudent(reservedStudentId))
			.Code.ShouldBe(StudentOnboardingErrorCodes.ExistingStudentInvalid);

		applicant.ExistingStudentId.ShouldBe(actualStudentId);
		applicant.ReservedStudentId.ShouldBe(reservedStudentId);
	}

	private static AdmissionApplicant CreateApplicant(Guid reservedStudentId, Guid? existingStudentId = null) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Asha", null, "Rao",
			new DateTime(2018, 1, 1), "Female", existingStudentId, reservedStudentId);
}
