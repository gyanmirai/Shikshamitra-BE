using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPrimaryContactSelection_Tests
{
	[Fact]
	public void Anonymous_guardian_confirmation_never_authorizes_qualification()
	{
		var applicant = Applicant();
		AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Anita", "9876543210", null, true, Guid.NewGuid(), DateTime.UtcNow, true);
		AdmissionPrimaryContactSelection.ReadRelationship(applicant).ShouldBe("guardian");
		AdmissionPrimaryContactSelection.GuardianResponsibilityConfirmed(applicant).ShouldBeFalse();
		Should.Throw<BusinessException>(() => AdmissionPrimaryContactSelection.EnsureReady(applicant));
		AdmissionSiblingParentSelection.IsParentFlow(applicant).ShouldBeFalse();
		AdmissionSiblingParentSelection.ReadRelationship(applicant).ShouldBeNull();
	}

	[Theory]
	[InlineData(false, true)]
	[InlineData(true, false)]
	public void Staff_guardian_fallback_needs_explicit_confirmation_and_an_authenticated_actor(bool confirmed, bool authenticated)
	{
		Should.Throw<BusinessException>(() => AdmissionPrimaryContactSelection.Capture(Applicant(), "guardian", "Anita", "9876543210", null,
			confirmed, authenticated ? Guid.NewGuid() : null, DateTime.UtcNow, false));
	}

	[Fact]
	public void Staff_can_confirm_public_guardian_without_creating_sibling_parent_authority()
	{
		var applicant = Applicant();
		AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Anita", "9876543210", "aunt@example.com", false, null, DateTime.UtcNow, true);
		AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Anita", "9876543210", "aunt@example.com", true, Guid.NewGuid(), DateTime.UtcNow, false);
		AdmissionPrimaryContactSelection.EnsureReady(applicant);
		AdmissionPrimaryContactSelection.GuardianResponsibilityConfirmed(applicant).ShouldBeTrue();
		AdmissionStudentProfileMapping.Read(applicant, "Guardian.Email").ShouldBe("aunt@example.com");
		AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldBeNull();
	}

	[Fact]
	public void Public_mother_is_captured_without_confirming_an_existing_shared_parent()
	{
		var applicant = Applicant();
		AdmissionPrimaryContactSelection.Capture(applicant, "mother", "Neha", "9876543210", null, false, null, DateTime.UtcNow, true);
		AdmissionStudentProfileMapping.Read(applicant, "Mother.Name").ShouldBe("Neha");
		AdmissionStudentProfileMapping.Read(applicant, "Father.Name").ShouldBeNull();
		AdmissionSiblingParentSelection.ReadConfirmation(applicant).ShouldBeNull();
		AdmissionPrimaryContactSelection.EnsureReady(applicant);
		AdmissionSiblingParentSelection.HasParentFlowProvenance(applicant).ShouldBeFalse();
	}

	[Fact]
	public void Untouched_legacy_generic_contact_does_not_gain_a_new_blocker()
	{
		AdmissionPrimaryContactSelection.EnsureReady(Applicant());
	}

	[Fact]
	public void Changing_away_from_guardian_clears_confirmation_and_requires_a_new_acknowledgement()
	{
		var applicant = Applicant();
		AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Anita", "9876543210", null, true, Guid.NewGuid(), DateTime.UtcNow, false);
		AdmissionPrimaryContactSelection.Capture(applicant, "mother", "Neha", "9876543211", null, false, Guid.NewGuid(), DateTime.UtcNow, false);
		AdmissionPrimaryContactSelection.GuardianResponsibilityConfirmed(applicant).ShouldBeFalse();
		Should.Throw<BusinessException>(() => AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Anita", "9876543210", null, false, Guid.NewGuid(), DateTime.UtcNow, false));
		AdmissionPrimaryContactSelection.ReadRelationship(applicant).ShouldBe("mother");
	}

	[Fact]
	public void Confirmed_sibling_parent_identity_is_never_replaced_by_a_primary_guardian_capture()
	{
		var applicant = Applicant();
		var source = Guid.NewGuid();
		AdmissionSiblingParentSelection.Confirm(applicant, new() { SourceApplicantId = source, Relationship = "father", Name = "Saved father", Mobile = "9876543210", Email = "father@example.com" }, Guid.NewGuid(), DateTime.UtcNow, "9876543210", "father@example.com");
		AdmissionPrimaryContactSelection.Capture(applicant, "guardian", "Another adult", "9876543212", "other@example.com", true, Guid.NewGuid(), DateTime.UtcNow, false);
		AdmissionSiblingParentSelection.ReadConfirmation(applicant)!.SourceApplicantId.ShouldBe(source);
		AdmissionPrimaryContactSelection.ReadRelationship(applicant).ShouldBe("father");
		AdmissionStudentProfileMapping.Read(applicant, "Father.Email").ShouldBe("father@example.com");
	}

	[Fact]
	public void New_optional_main_guardian_needs_confirmation_while_unselected_guardian_does_not()
	{
		var applicant = Applicant();
		AdmissionPrimaryContactSelection.CaptureMainGuardian(applicant, new() { MainContactRelationship = "father", OtherGuardian = new() { FullName = "Aunt", Relationship = "Aunt" } }, false, Guid.NewGuid(), DateTime.UtcNow, false);
		Should.Throw<BusinessException>(() => AdmissionPrimaryContactSelection.CaptureMainGuardian(applicant,
			new() { MainContactRelationship = "guardian" }, false, Guid.NewGuid(), DateTime.UtcNow, false));
	}

	[Fact]
	public void Optional_main_guardian_confirmation_preserves_sibling_parent_account_proof()
	{
		var applicant = Applicant();
		var source = Guid.NewGuid();
		AdmissionSiblingParentSelection.Confirm(applicant, new() { SourceApplicantId = source, Relationship = "father", Name = "Saved father", Mobile = "9876543210" }, Guid.NewGuid(), DateTime.UtcNow, "9876543210", null);
		AdmissionPrimaryContactSelection.CaptureMainGuardian(applicant, new() { MainContactRelationship = "guardian" }, true, Guid.NewGuid(), DateTime.UtcNow, false);
		AdmissionPrimaryContactSelection.ReadRelationship(applicant).ShouldBe("guardian");
		AdmissionSiblingParentSelection.ReadConfirmation(applicant)!.SourceApplicantId.ShouldBe(source);
		AdmissionSiblingParentSelection.ReadRelationship(applicant).ShouldBe("father");
		AdmissionPrimaryContactSelection.EnsureReady(applicant);
	}

	private static AdmissionApplicant Applicant() => new(Guid.NewGuid(), null, Guid.NewGuid(), "Child", null, null, null, null, null, Guid.NewGuid());
}
