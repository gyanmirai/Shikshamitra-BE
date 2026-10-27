using System;
using ShikshaMitra.Students.Records;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFamilyContactCapture_Tests
{
	[Fact]
	public void Optional_guardian_and_second_parent_survive_student_handoff_without_replacing_first_parent()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Rajesh Sharma", "9876543210", "father@example.com");
		AdmissionFamilyContactCapture.Capture(applicant, new()
		{
			OtherParent = new() { Relationship = "mother", FullName = "Meena Sharma", Email = "mother@example.com" },
			OtherGuardian = new() { Relationship = "Aunt", FullName = "Anita Sharma", Mobile = "9876543212", Email = "aunt@example.com" },
			MainContactRelationship = "guardian"
		}, "father");
		var student = NewStudent(applicant);
		AdmissionStudentProfileMapping.Apply(applicant, student);
		student.FamilyDetail!.FatherDetails!.Email.ShouldBe("father@example.com");
		student.FamilyDetail.MotherDetails!.Email.ShouldBe("mother@example.com");
		student.FamilyDetail.GuardianDetails!.Email.ShouldBe("aunt@example.com");
		StudentFamilyContactSelection.Resolve(student).ShouldBe("guardian");
		student.ExtraProperties[StudentFamilyContactSelection.GuardianRelationshipKey].ShouldBe("Aunt");
		AdmissionSiblingParentSelection.ReadRelationship(applicant).ShouldBe("father");
		student.UserId.ShouldBeNull();
	}

	[Fact]
	public void Adding_guardian_does_not_change_default_parent_or_borrow_parent_email()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "mother", "Meena", "9876543210", "mother@example.com");
		AdmissionFamilyContactCapture.Capture(applicant, new()
		{
			OtherGuardian = new() { Relationship = "Uncle", FullName = "Amit", Mobile = "9876543212" },
			MainContactRelationship = "mother"
		}, "mother");
		var student = NewStudent(applicant);
		AdmissionStudentProfileMapping.Apply(applicant, student);
		StudentFamilyContactSelection.Resolve(student).ShouldBe("mother");
		student.FamilyDetail!.GuardianDetails!.Email.ShouldBeNull();
	}

	[Fact]
	public void Additional_parent_cannot_overwrite_confirmed_sibling_parent_proof()
	{
		var applicant = Applicant();
		var source = Guid.NewGuid();
		AdmissionSiblingParentSelection.Confirm(applicant, new() { SourceApplicantId = source, Relationship = "father", Name = "Rajesh", FirstName = "Rajesh", Mobile = "9876543210", Email = "father@example.com" }, Guid.NewGuid(), DateTime.UtcNow, "9876543210", "father@example.com");
		Should.Throw<BusinessException>(() => AdmissionFamilyContactCapture.Capture(applicant, new()
		{
			OtherParent = new() { Relationship = "father", FullName = "Different father", Email = "different@example.com" }, MainContactRelationship = "father"
		}, "father"));
		AdmissionSiblingParentSelection.ReadConfirmation(applicant)!.SourceApplicantId.ShouldBe(source);
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe("father@example.com");
	}

	[Fact]
	public void Missing_main_guardian_is_rejected_without_creating_a_profile()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Rajesh", "9876543210", null);
		Should.Throw<BusinessException>(() => AdmissionFamilyContactCapture.Capture(applicant, new() { MainContactRelationship = "guardian" }, "father"));
		AdmissionStudentProfileMapping.Read(applicant, "Guardian.Name").ShouldBeNull();
	}

	[Fact]
	public void Main_contact_only_change_does_not_require_portal_access_repair()
	{
		const string before = "{\"fatherDetails\":{\"fullName\":\"Rajesh\",\"email\":\"father@example.com\"},\"guardianDetails\":{\"fullName\":\"Anita\",\"email\":\"aunt@example.com\"},\"mainContactRelationship\":\"father\",\"guardianRelationship\":\"Aunt\",\"siblingStudentIds\":[\"same-sibling\"]}";
		var after = before.Replace("\"mainContactRelationship\":\"father\"", "\"mainContactRelationship\":\"guardian\"");
		StudentFamilyContactSelection.RequiresAccessReview(before, after).ShouldBeFalse();
		StudentFamilyContactSelection.RequiresAccessReview(before, after.Replace("father@example.com", "changed@example.com")).ShouldBeTrue();
	}

	private static AdmissionApplicant Applicant() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Rahul", null, "Sharma", new DateTime(2018, 5, 15), "Male", null, Guid.NewGuid());

	[Fact]
	public void School_communication_uses_selected_guardians_own_contact_without_parent_email_fallback()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Rajesh", "9876543210", "father@example.com");
		AdmissionFamilyContactCapture.Capture(applicant, new()
		{
			MainContactRelationship = "guardian",
			OtherGuardian = new() { FullName = "Anita", Relationship = "Aunt", Mobile = "9876543212" }
		}, "father");
		var contact = AdmissionFamilyContactCapture.ReadMainContact(applicant).ShouldNotBeNull();
		contact.FullName.ShouldBe("Anita");
		contact.Relationship.ShouldBe("guardian");
		contact.RelationshipLabel.ShouldBe("Aunt");
		contact.Mobile.ShouldBe("9876543212");
		contact.Email.ShouldBeNull();
	}

	[Fact]
	public void Contact_retry_cannot_silently_change_the_original_main_contact()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Rajesh", "9876543210", "father@example.com");
		var input = new AdmissionFamilyContactsDto
		{
			MainContactRelationship = "father", OtherGuardian = new() { FullName = "Anita", Relationship = "Aunt", Mobile = "9876543212" }
		};
		AdmissionFamilyContactCapture.Capture(applicant, input, "father");
		AdmissionFamilyContactCapture.MatchesRequest(applicant, input, "father").ShouldBeTrue();
		input.MainContactRelationship = "guardian";
		AdmissionFamilyContactCapture.MatchesRequest(applicant, input, "father").ShouldBeFalse();
		AdmissionFamilyContactCapture.MatchesRequest(applicant, null, "father").ShouldBeFalse();
		input.MainContactRelationship = "father";
		input.OtherGuardian!.FullName = null!;
		AdmissionFamilyContactCapture.MatchesRequest(applicant, input, "father").ShouldBeFalse();
	}
	private static Student NewStudent(AdmissionApplicant applicant) => Student.CreateDirectRegistration(applicant.ReservedStudentId, applicant.TenantId, "Rahul", null, "Sharma",
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, new DateTime(2018, 5, 15), GenderEnum.Male, "9876543210");
}
