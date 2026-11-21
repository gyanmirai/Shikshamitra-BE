using System;
using ShikshaMitra.Library.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryNotificationDispatchState_Tests
{
	private static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Claimed_Intent_Cannot_Be_Claimed_Twice()
	{
		var row = Queued();
		var claim = Guid.NewGuid();
		row.Claim(Now, claim);
		Should.Throw<BusinessException>(() => row.Claim(Now, Guid.NewGuid()));
		row.HasClaim(claim).ShouldBeTrue();
		row.AttemptCount.ShouldBe(1);
	}

	[Fact]
	public void Definite_Temporary_Rejection_Has_A_Bounded_Retry_Budget()
	{
		var row = Queued();
		var due = Now;
		for (var attempt = 1; attempt <= 3; attempt++)
		{
			row.Claim(due, Guid.NewGuid());
			row.RecordFailure(due, acceptanceUnknown: false, transient: true);
			row.AttemptCount.ShouldBe(attempt);
			if (attempt < 3)
			{
				row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
				row.NextAttemptAt.ShouldNotBeNull();
				due = row.NextAttemptAt.Value;
			}
		}
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Failed);
		row.NextAttemptAt.ShouldBeNull();
		row.SentAt.ShouldBeNull();
	}

	[Fact]
	public void Uncertain_Acceptance_Requires_Explicit_Duplicate_Acknowledgement()
	{
		var row = Queued();
		row.Claim(Now, Guid.NewGuid());
		row.RecordFailure(Now, acceptanceUnknown: true, transient: true);
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Unconfirmed);
		row.NextAttemptAt.ShouldBeNull();
		Should.Throw<BusinessException>(() => row.QueueForDelivery(Now)).Code
			.ShouldBe(LibraryNotificationErrorCodes.DuplicateAcknowledgementRequired);
		row.QueueForDelivery(Now, acknowledgeUnconfirmed: true);
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Queued);
	}

	[Fact]
	public void Interrupted_Transport_Is_Recovered_As_Unconfirmed_Without_Automatic_Resend()
	{
		var row = Queued();
		row.Claim(Now, Guid.NewGuid());
		row.RecoverInterrupted(Now.AddMinutes(9));
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Processing);
		row.RecoverInterrupted(Now.AddMinutes(11));
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Unconfirmed);
		row.NextAttemptAt.ShouldBeNull();
	}

	[Fact]
	public void Accepted_Email_Cannot_Be_Requeued_And_Old_Claims_Cannot_Finalize_A_New_Attempt()
	{
		var row = Queued();
		var previousClaim = Guid.NewGuid();
		row.Claim(Now, previousClaim);
		row.RecordFailure(Now, acceptanceUnknown: false, transient: false);
		row.QueueForDelivery(Now);
		var currentClaim = Guid.NewGuid();
		row.Claim(Now, currentClaim);
		row.HasClaim(previousClaim).ShouldBeFalse();
		row.HasClaim(currentClaim).ShouldBeTrue();
		row.MarkSent(Now);
		row.Status.ShouldBe(LibraryNotificationDeliveryStatuses.Sent);
		row.SentAt.ShouldBe(Now);
		Should.Throw<BusinessException>(() => row.QueueForDelivery(Now));
	}

	private static LibraryNotificationHistory Queued()
	{
		var row = new LibraryNotificationHistory(Guid.NewGuid(), Guid.NewGuid(), LibraryNotificationEventTypes.DueDateReminder,
			LibraryNotificationEventGroups.Transactions, "Due reminder", LibraryNotificationChannels.Email,
			LibraryNotificationRecipientRoles.Member, null, "Member", "member@example.test", "Due", "Please return your book.",
			LibraryNotificationDeliveryStatuses.Queued, null, null, null, null, null, null);
		row.QueueForDelivery(Now);
		return row;
	}
}
