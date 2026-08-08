using System;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.Enrollment;

public class AdmissionCrmHouseholdIdentity_Tests
{
	[Fact]
	public void Verified_core_identity_cannot_be_silently_replaced_by_unverified_input()
	{
		var person = new AdmissionCrmPerson(
			Guid.NewGuid(), Guid.NewGuid(), "Aarav", null, "Patel", new DateTime(2018, 4, 10),
			"9876543210", null, AdmissionCrmPersonProvenance.DocumentVerified);
		person.SetCoreIdentity(
			"Aarav", null, "Patel", new DateTime(2018, 4, 10), "9876543210", null,
			AdmissionCrmPersonProvenance.DocumentVerified, true, DateTime.UtcNow);

		Should.Throw<BusinessException>(() => person.SetCoreIdentity(
			"Arav", null, "Patel", new DateTime(2018, 4, 10), "9876543210", null,
			AdmissionCrmPersonProvenance.ApplicantSupplied, false, null))
			.Code.ShouldBe("AdmissionCrm:VerifiedPersonIdentityCannotBeOverwritten");
	}

	[Fact]
	public void Applicant_cannot_be_their_own_guardian()
	{
		var personId = Guid.NewGuid();
		Should.Throw<BusinessException>(() => new AdmissionCrmGuardianRelationship(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			personId, personId, AdmissionCrmGuardianRelationshipType.LegalGuardian, DateTime.UtcNow))
			.Code.ShouldBe("AdmissionCrm:ApplicantCannotBeOwnGuardian");
	}

	[Fact]
	public void Identity_readiness_requires_all_configured_inputs_unless_reasoned_override_is_used()
	{
		var profile = new AdmissionCrmApplicantIdentityProfile(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		profile.Evaluate(accountableContactReady: true, identityConsentReady: true, accountLinkReady: false, conflictReason: null);
		profile.Status.ShouldBe(AdmissionCrmIdentityResolutionStatus.PendingReview);

		profile.Override("Principal approved temporary account-link repair.", Guid.NewGuid(), DateTime.UtcNow);
		profile.Evaluate(accountableContactReady: true, identityConsentReady: true, accountLinkReady: false, conflictReason: null);
		profile.Status.ShouldBe(AdmissionCrmIdentityResolutionStatus.Overridden);
		profile.OverrideUsed.ShouldBeTrue();
	}

	[Fact]
	public void Guardian_responsibilities_support_more_than_a_parent_label()
	{
		var relationship = new AdmissionCrmGuardianRelationship(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), AdmissionCrmGuardianRelationshipType.Grandparent, DateTime.UtcNow);
		relationship.ConfigureResponsibilities(
			primary: true, emergency: true, accountable: true, feeResponsibility: false,
			pickupAuthorization: true, portalAccess: true, communicationPriority: 1);

		relationship.IsAccountableContact.ShouldBeTrue();
		relationship.HasPickupAuthorization.ShouldBeTrue();
		relationship.HasFeeResponsibility.ShouldBeFalse();
	}

	[Theory]
	[InlineData("abc1", null, "AdmissionCrm:InvalidContactNumber")]
	[InlineData("9999900807", "not-an-email", "AdmissionCrm:InvalidEmail")]
	public void Person_identity_rejects_malformed_contact_data(
		string phone,
		string? email,
		string expectedCode)
	{
		Should.Throw<BusinessException>(() => new AdmissionCrmPerson(
			Guid.NewGuid(), Guid.NewGuid(), "Aarav", null, "Patel", new DateTime(2018, 4, 10),
			phone, email, AdmissionCrmPersonProvenance.StaffSupplied))
			.Code.ShouldBe(expectedCode);
	}

	[Fact]
	public void Person_identity_rejects_future_date_of_birth()
	{
		Should.Throw<BusinessException>(() => new AdmissionCrmPerson(
			Guid.NewGuid(), Guid.NewGuid(), "Aarav", null, "Patel", DateTime.UtcNow.Date.AddDays(1),
			"9999900807", null, AdmissionCrmPersonProvenance.StaffSupplied))
			.Code.ShouldBe("AdmissionCrm:FutureDateOfBirth");
	}
}
