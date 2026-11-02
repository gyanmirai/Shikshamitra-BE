using System;
using System.Collections.Generic;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFirstParentContactMapping_Tests
{
	[Fact]
	public void Main_guardian_choice_does_not_redirect_existing_parent_registration_aliases_to_guardian()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Father", "9876543210", "old@example.com");
		AdmissionPrimaryContactSelection.CaptureMainGuardian(applicant, new() { MainContactRelationship = "guardian" }, true, Guid.NewGuid(), DateTime.UtcNow, false);
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "father@example.com" });
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe("father@example.com");
		AdmissionStudentProfileMapping.Read(applicant, "Guardian.Email").ShouldBeNull();
		AdmissionPrimaryContactSelection.ReadRelationship(applicant).ShouldBe("guardian");
	}
	[Theory]
	[InlineData("father", "Father")]
	[InlineData("mother", "Mother")]
	public void Public_primary_parent_accepts_later_registration_aliases_without_gaining_sibling_account_authority(string role, string prefix)
	{
		var applicant = Applicant();
		AdmissionPrimaryContactSelection.Capture(applicant, role, "Primary adult", "9876543210", null, false, null, DateTime.UtcNow, true);
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "public-parent@example.com" });
		AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email").ShouldBe("public-parent@example.com");
		AdmissionSiblingParentSelection.HasParentFlowProvenance(applicant).ShouldBeFalse();
		AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldBeNull();
	}
	[Theory]
	[InlineData("father", "Father")]
	[InlineData("mother", "Mother")]
	public void First_parent_registration_alias_maps_email_to_only_selected_role(string role, string prefix)
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, role, "Ravi Patel", "9876543210", null);
		applicant.SetProperty("AdmissionStudentField.Applicant.Email", "student@example.com");
		var otherPrefix = prefix == "Father" ? "Mother" : "Father";
		applicant.SetProperty("AdmissionStudentField." + otherPrefix + ".Email", "other@example.com");
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = " PARENT@EXAMPLE.COM " });
		AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email").ShouldBe("parent@example.com");
		AdmissionStudentProfileMapping.Read(applicant, otherPrefix + ".Email").ShouldBe("other@example.com");
		AdmissionStudentProfileMapping.Read(applicant, "Applicant.Email").ShouldBe("student@example.com");
	}

	[Theory]
	[InlineData("father", "Father", "explicit@example.com")]
	[InlineData("mother", "Mother", "")]
	public void First_parent_structured_answer_and_clear_remain_authoritative_across_stages(string role, string prefix, string explicitEmail)
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, role, "Ravi Patel", "9876543210", "initial@example.com");
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string>
		{
			["Guardian.Email"] = "generic@example.com", [prefix + ".Email"] = explicitEmail
		});
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "later@example.com" });
		AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email").ShouldBe(string.IsNullOrEmpty(explicitEmail) ? null : explicitEmail);
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string>
		{
			["Guardian.Email"] = "stale@example.com", [prefix + ".Email"] = "older-structured@example.com"
		}, recoverSavedAnswers: true);
		AdmissionStudentProfileMapping.Read(applicant, prefix + ".Email").ShouldBe(string.IsNullOrEmpty(explicitEmail) ? null : explicitEmail);
	}

	[Fact]
	public void First_parent_generic_contact_corrections_and_clear_are_not_restored_by_saved_answer_recovery()
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Ravi Patel", "9876543210", null);
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "first@example.com" });
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string>
		{
			["Guardian.Email"] = "corrected@example.com", ["Guardian.Mobile"] = "9876543212", ["Guardian.FirstName"] = "Ramesh"
		});
		AdmissionStudentProfileMapping.Read(applicant, "Father.Name").ShouldBe("Ramesh Patel");
		AdmissionStudentProfileMapping.Read(applicant, "Father.Mobile").ShouldBe("9876543212");
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe("corrected@example.com");
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "" });
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "stale@example.com" }, recoverSavedAnswers: true);
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBeNull();
	}

	[Theory]
	[InlineData("legacy")]
	[InlineData("return")]
	[InlineData("sibling")]
	public void First_parent_mapping_does_not_change_legacy_return_or_confirmed_sibling(string flow)
	{
		var applicant = Applicant(flow == "return" ? Guid.NewGuid() : null);
		if (flow != "legacy") AdmissionSiblingParentSelection.CaptureParent(applicant, "father", "Ravi Patel", "9876543210", "saved@example.com");
		if (flow == "sibling") AdmissionSiblingParentSelection.Confirm(applicant, new()
		{
			SourceApplicantId = Guid.NewGuid(), Relationship = "father", Name = "Ravi Patel", FirstName = "Ravi", LastName = "Patel",
			Email = "saved@example.com", Mobile = "9876543210"
		}, Guid.NewGuid(), DateTime.UtcNow, "9876543210", "saved@example.com");
		var before = System.Text.Json.JsonSerializer.Serialize(applicant.ExtraProperties);
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string>
		{
			["Guardian.Email"] = "changed@example.com", ["Father.Email"] = "explicit@example.com"
		});
		System.Text.Json.JsonSerializer.Serialize(applicant.ExtraProperties).ShouldBe(before);
	}

	[Theory]
	[InlineData("father", "Father")]
	[InlineData("mother", "Mother")]
	public void First_parent_pre_fix_saved_alias_repairs_canonical_student_handoff(string role, string prefix)
	{
		var applicant = Applicant();
		AdmissionSiblingParentSelection.CaptureParent(applicant, role, "Ravi Patel", "9876543210", null);
		AdmissionFirstParentContactMapping.Apply(applicant, new Dictionary<string, string> { ["Guardian.Email"] = "parent@example.com" }, recoverSavedAnswers: true);
		var student = Student.CreateDirectRegistration(applicant.ReservedStudentId, applicant.TenantId, "Asha", null, "Patel",
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, new DateTime(2018, 5, 15), GenderEnum.Female, "9876543210");
		AdmissionStudentProfileMapping.Apply(applicant, student);
		(prefix == "Father" ? student.FamilyDetail?.FatherDetails : student.FamilyDetail?.MotherDetails).ShouldNotBeNull().Email.ShouldBe("parent@example.com");
		student.FamilyDetail?.GuardianDetails.ShouldBeNull();
	}

	private static AdmissionApplicant Applicant(Guid? existingStudentId = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"Asha", null, "Patel", new DateTime(2018, 5, 15), "Female", existingStudentId, Guid.NewGuid());
}
