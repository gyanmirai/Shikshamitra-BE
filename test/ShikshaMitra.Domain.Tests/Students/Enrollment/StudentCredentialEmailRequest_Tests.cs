using System;
using ShikshaMitra.Students.Portal;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentCredentialEmailRequest_Tests
{
	private static readonly DateTime Now = new(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);
	private static StudentCredentialEmailRequest NewRequest() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "guardian", "parent@example.test", Guid.NewGuid(), Now);
	[Fact]
	public void A_durable_claim_cannot_be_claimed_twice()
	{
		var row = NewRequest(); row.Claim(Now);
		row.Status.ShouldBe("Processing"); row.AttemptCount.ShouldBe(1);
		Should.Throw<InvalidOperationException>(() => row.Claim(Now));
		row.CanRequest(Now.AddHours(1)).ShouldBeFalse();
	}
	[Fact]
	public void Sending_failure_preserves_encrypted_initial_password_for_controlled_retry()
	{
		var row = NewRequest(); row.Claim(Now); row.SetInitialPassword("encrypted-secret"); row.Failed(Now);
		row.Status.ShouldBe("Failed"); row.EncryptedInitialPassword.ShouldBe("encrypted-secret");
		row.CanRequest(Now.AddMinutes(1)).ShouldBeFalse(); row.CanRequest(Now.AddMinutes(2)).ShouldBeTrue();
		row.Requeue(Guid.NewGuid(), Now.AddMinutes(2)); row.Claim(Now.AddMinutes(2));
		row.AttemptCount.ShouldBe(2); row.EncryptedInitialPassword.ShouldBe("encrypted-secret");
		row.Sent(Now.AddMinutes(2)); row.EncryptedInitialPassword.ShouldBeNull();
	}
	[Fact]
	public void Cancellation_discards_secrets_but_allows_a_fresh_authorized_request_after_cooldown()
	{
		var row = NewRequest(); row.SetInitialPassword("encrypted-secret"); row.Cancel("Student access revoked.");
		row.Status.ShouldBe("Cancelled"); row.EncryptedInitialPassword.ShouldBeNull();
		row.CanRequest(Now.AddMinutes(1)).ShouldBeFalse(); row.CanRequest(Now.AddDays(1)).ShouldBeTrue();
		row.Requeue(Guid.NewGuid(), Now.AddDays(1)); row.Status.ShouldBe("Pending"); row.EncryptedInitialPassword.ShouldBeNull();
	}
	[Fact]
	public void Interrupted_transport_requires_mailbox_review_before_manual_retry()
	{
		var row = NewRequest(); row.Claim(Now); row.RecoverInterrupted(Now.AddMinutes(9));
		row.Status.ShouldBe("Processing"); row.RecoverInterrupted(Now.AddMinutes(10));
		row.Status.ShouldBe("Failed"); row.Message.ShouldNotBeNull().ShouldContain("mailbox"); row.IsDue(Now.AddMinutes(10)).ShouldBeFalse();
	}
	[Fact]
	public void Sent_resend_retains_delivery_evidence_but_has_no_old_password()
	{
		var row = NewRequest(); row.Claim(Now); row.SetInitialPassword("encrypted-secret"); row.Sent(Now);
		row.Requeue(Guid.NewGuid(), Now.AddMinutes(2));
		row.SentAt.ShouldBe(Now); row.EncryptedInitialPassword.ShouldBeNull(); row.IsDue(Now.AddMinutes(2)).ShouldBeTrue();
	}
	[Fact]
	public void Completion_wait_preserves_pending_intent_without_claim_or_duplicate_request()
	{
		var row = NewRequest(); row.RequireSavedOwnerValidation(); row.DeferUntilCompletion(Now);
		row.RequiresSavedOwnerValidation().ShouldBeTrue(); row.Status.ShouldBe("Pending"); row.AttemptCount.ShouldBe(0);
		row.EncryptedInitialPassword.ShouldBeNull(); row.ClaimedAt.ShouldBeNull();
		row.IsDue(Now).ShouldBeFalse(); row.IsDue(Now.AddMinutes(1)).ShouldBeTrue();
		row.CanRequest(Now.AddHours(1)).ShouldBeFalse();
		row.Claim(Now.AddMinutes(1));
		Should.Throw<InvalidOperationException>(() => row.DeferUntilCompletion(Now.AddMinutes(1)));
	}
	[Fact]
	public void A_later_legacy_resend_does_not_inherit_the_manual_delivery_override()
	{
		var row = NewRequest(); row.RequireSavedOwnerValidation(); row.Claim(Now); row.Sent(Now);
		row.Requeue(Guid.NewGuid(), Now.AddMinutes(2)); row.RequiresSavedOwnerValidation().ShouldBeFalse();
		row.RequireSavedOwnerValidation(); row.RequiresSavedOwnerValidation().ShouldBeTrue();
	}
	[Fact]
	public void Owner_verified_recipient_change_drops_stale_secret_and_requires_a_new_manual_intent()
	{
		var row = NewRequest(); var previous = row.Email;
		row.RequireSavedOwnerValidation(); row.Claim(Now); row.SetInitialPassword("encrypted-secret"); row.Failed(Now);
		Should.Throw<InvalidOperationException>(() => row.RequeueForRecipient(Guid.NewGuid(), Now.AddMinutes(1), "new@example.test"));
		row.Email.ShouldBe(previous);
		row.RequeueForRecipient(Guid.NewGuid(), Now.AddMinutes(2), "new@example.test");
		row.Email.ShouldBe("new@example.test"); row.EncryptedInitialPassword.ShouldBeNull();
		row.ExtraProperties["PreviousRecipientEmail"].ShouldBe(previous);
		row.RequiresSavedOwnerValidation().ShouldBeFalse(); row.RequireSavedOwnerValidation();
	}
}
