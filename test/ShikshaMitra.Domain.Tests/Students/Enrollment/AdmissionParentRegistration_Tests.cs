using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionParentRegistration_Tests
{
	[Fact]
	public void Parent_evidence_has_nullable_staff_actor_and_explicit_invitation_provenance()
	{
		typeof(AdmissionDocumentEvidence).GetProperty("SubmittedBy")!.PropertyType.ShouldBe(typeof(Guid?));
		typeof(AdmissionDocumentEvidence).GetProperty("ParentRegistrationId").ShouldNotBeNull();
	}
	[Fact]
	public void Parent_file_retains_truthful_null_staff_actor_and_contact_provenance()
	{
		var id = Guid.NewGuid(); var now = DateTime.UtcNow;
		var file = new AdmissionDocumentFile("proof.pdf", "application/pdf", 10, new string('A', 64), "private/file", null);
		var row = new AdmissionDocumentEvidence(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			new() { Code = "PROOF", Name = "Proof" }, 1, "upload", null, now, "{}", file, parentRegistrationId: id, parentContactJson: "{\"FullName\":\"Mother\"}");
		row.SubmittedBy.ShouldBeNull(); row.GetFile()!.SubmittedBy.ShouldBeNull(); row.ParentRegistrationId.ShouldBe(id);
		Should.Throw<BusinessException>(() => new AdmissionDocumentEvidence(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			new() { Code = "PROOF", Name = "Proof" }, 1, "upload", null, now, "{}", file));
	}
	[Fact]
	public void Grant_expires_in_seven_days_and_hand_in_locks_without_an_application_transition()
	{
		var now = DateTime.UtcNow; var grant = Grant(now);
		grant.ExpiresAt.ShouldBe(now.AddDays(7)); grant.CanWrite(now.AddDays(7)).ShouldBeFalse();
		grant.Save(now); grant.Status.ShouldBe("InProgress"); grant.HandIn(now);
		grant.Status.ShouldBe("ReadyForReview"); grant.CanRead(now).ShouldBeTrue(); grant.CanWrite(now).ShouldBeFalse();
		Should.Throw<BusinessException>(() => grant.Save(now));
		grant.Close("TakenOver"); grant.CanRead(now).ShouldBeFalse(); grant.ProtectedToken.ShouldBeEmpty();
	}
	[Fact]
	public void Only_initial_unfinished_grants_can_be_reactivated_after_their_expiry()
	{
		var now = DateTime.UtcNow;
		var expiry = now.AddDays(7);
		var active = Grant(now);
		active.CanReactivate(now).ShouldBeFalse();
		active.CanReactivate(expiry).ShouldBeTrue();
		var handedIn = Grant(now);
		handedIn.HandIn(now);
		handedIn.CanReactivate(expiry).ShouldBeFalse();
		var correction = Grant(now, purpose: "correction");
		correction.CanReactivate(expiry).ShouldBeFalse();
		foreach (var status in new[] { "Revoked", "TakenOver", "Applied", "Closed" })
		{
			var closed = Grant(now);
			closed.Close(status);
			closed.CanReactivate(expiry).ShouldBeFalse();
		}
	}
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void Validity_uses_the_same_expiry_boundary_for_utc_and_local_clock_values(bool local)
	{
		var now = DateTime.UtcNow;
		var grant = Grant(local ? now.ToLocalTime() : now, validityDays: 1);
		grant.ExpiresAt.ShouldBe(now.AddDays(1));
		grant.CanRead(now.AddHours(23).ToLocalTime()).ShouldBeTrue();
		grant.CanReactivate(now.AddHours(24).ToLocalTime()).ShouldBeTrue();
	}
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MaxValue)]
	public void Grant_rejects_validity_outside_the_supported_range(int days)
	{
		var now = DateTime.UtcNow;
		Should.Throw<BusinessException>(() => Grant(now, validityDays: days)).Code.ShouldBe(AdmissionParentRegistrationErrors.Invalid);
		var grant = Grant(now);
		var expired = now.AddDays(7);
		Should.Throw<BusinessException>(() => grant.Reactivate(expired, days)).Code.ShouldBe(AdmissionParentRegistrationErrors.Invalid);
		grant.ExpiresAt.ShouldBe(expired);
	}
	[Fact]
	public void Retry_key_is_bound_to_exact_command_and_preserves_previous_results()
	{
		var grant = Grant(DateTime.UtcNow);
		grant.RecordOperation("same-key", "same-payload");
		grant.IsReplay("same-key", "same-payload").ShouldBeTrue();
		Should.Throw<BusinessException>(() => grant.IsReplay("same-key", "different-payload")).Code.ShouldBe(AdmissionParentRegistrationErrors.Conflict);
	}
	private static AdmissionParentRegistration Grant(DateTime now, string purpose = "initial", int? validityDays = null) =>
		new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), "encrypted",
			"{}", "fingerprint", purpose, now, "request", false, validityDays: validityDays);
}
