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
	public void Retry_key_is_bound_to_exact_command_and_preserves_previous_results()
	{
		var grant = Grant(DateTime.UtcNow);
		grant.RecordOperation("same-key", "same-payload");
		grant.IsReplay("same-key", "same-payload").ShouldBeTrue();
		Should.Throw<BusinessException>(() => grant.IsReplay("same-key", "different-payload")).Code.ShouldBe(AdmissionParentRegistrationErrors.Conflict);
	}
	private static AdmissionParentRegistration Grant(DateTime now) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		new string('A', 64), "encrypted", "{}", "fingerprint", "initial", now, "request", false);
}
