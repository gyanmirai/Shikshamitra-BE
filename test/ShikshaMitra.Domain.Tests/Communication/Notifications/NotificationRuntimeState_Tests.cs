using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationRuntimeState_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private const string Snapshot = "{\"schemaVersion\":1,\"channels\":[3],\"fallbackLanguage\":\"en\"}";

	[Fact]
	public void Snapshot_and_planning_changes_advance_the_aggregate_concurrency_fence()
	{
		var row = Intent(); var before = row.ConcurrencyStamp;
		row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.Automatic);
		row.ConcurrencyStamp.ShouldNotBe(before); before = row.ConcurrencyStamp;
		var lease = HydrateClaim(row, NotificationIntentState.Processing); row.CompletePlanning(lease, Now);
		row.ConcurrencyStamp.ShouldNotBe(before);
	}

	[Fact]
	public void Starting_io_and_recovering_a_crash_advance_the_aggregate_concurrency_fence()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing); var before = row.ConcurrencyStamp;
		row.BeginIo(lease, Now); row.ConcurrencyStamp.ShouldNotBe(before); before = row.ConcurrencyStamp;
		row.RecoverExpiredLease(Now.AddMinutes(6)); row.ConcurrencyStamp.ShouldNotBe(before);
	}

	[Fact]
	public void Intent_captures_an_immutable_rule_snapshot_without_rewriting_its_rule_revision()
	{
		var row = Intent(); row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.Automatic);
		row.RuleConfigurationJson.ShouldBe(Snapshot); row.RuleRevision.ShouldBe(2);
		Should.Throw<BusinessException>(() => row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.Automatic));
		row.RuleConfigurationJson.ShouldBe(Snapshot);
	}

	[Theory]
	[InlineData("{}")]
	[InlineData("[]")]
	[InlineData("not json")]
	[InlineData("{\"schemaVersion\":0}")]
	public void A_missing_or_malformed_snapshot_never_becomes_an_executable_intent(string snapshot)
	{
		var row = Intent();
		Should.Throw<BusinessException>(() => row.BindRuntimeSnapshot(snapshot, NotificationIntentOrigin.Automatic));
		row.RuleConfigurationJson.ShouldBe("{}");
	}

	[Fact]
	public void A_reviewed_manual_intent_needs_an_actor_and_its_server_owned_campaign_operation()
	{
		Should.Throw<BusinessException>(() => Intent().BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.ReviewedManual, Guid.NewGuid()));
		var actor = Guid.NewGuid(); var row = Intent(actor);
		Should.Throw<BusinessException>(() => row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.ReviewedManual));
		var operation = Guid.NewGuid(); row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.ReviewedManual, operation);
		row.Origin.ShouldBe(NotificationIntentOrigin.ReviewedManual); row.CampaignId.ShouldBe(operation);
	}

	[Fact]
	public void Planning_completion_cannot_claim_delivery_and_rejects_a_stale_lease()
	{
		var row = Intent(); row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.Automatic);
		var lease = HydrateClaim(row, NotificationIntentState.Processing);
		Should.Throw<BusinessException>(() => row.CompletePlanning(Guid.NewGuid(), Now));
		row.CompletePlanning(lease, Now);
		row.Status.ShouldBe(NotificationIntentState.AwaitingDelivery); row.LeaseId.ShouldBeNull();
		row.CompleteDelivery(NotificationIntentState.Completed); row.Status.ShouldBe(NotificationIntentState.Completed);
	}

	[Fact]
	public void An_unplanned_intent_cannot_be_marked_delivered()
	{
		Should.Throw<BusinessException>(() => Intent().CompleteDelivery(NotificationIntentState.Completed));
	}

	[Fact]
	public void Dispatch_planning_evidence_cannot_be_rebound_or_attached_after_claim()
	{
		var row = Dispatch();
		var campaign = Guid.NewGuid();
		var evidence = Guid.NewGuid();
		var provider = Guid.NewGuid();
		row.BindPlanningEvidence(campaign, evidence, provider);
		Should.Throw<BusinessException>(() => row.BindPlanningEvidence(Guid.NewGuid(), Guid.NewGuid(), null));
		row.CampaignId.ShouldBe(campaign);
		row.RecipientSnapshotId.ShouldBe(evidence);
		row.ProviderSettingId.ShouldBe(provider);

		var claimed = Dispatch();
		HydrateClaim(claimed, NotificationDispatchState.Processing);
		Should.Throw<BusinessException>(() => claimed.BindPlanningEvidence(campaign, evidence, provider));
		claimed.CampaignId.ShouldBeNull();
	}

	[Fact]
	public void Io_start_needs_the_current_active_claim_and_is_persisted_once()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		Should.Throw<BusinessException>(() => row.BeginIo(Guid.NewGuid(), Now));
		row.BeginIo(lease, Now);
		row.IoStartedAt.ShouldBe(Now); row.IoLeaseId.ShouldBe(lease);
		Should.Throw<BusinessException>(() => row.BeginIo(lease, Now));
	}

	[Fact]
	public void A_worker_must_not_start_io_after_its_claim_expires()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		Should.Throw<BusinessException>(() => row.BeginIo(lease, Now.AddMinutes(6)));
		row.IoStartedAt.ShouldBeNull();
	}

	[Fact]
	public void A_provider_outcome_requires_the_persisted_io_token()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		Should.Throw<BusinessException>(() => row.RecordOutcome(lease, Now, NotificationDispatchState.ProviderAccepted, "provider-id"));
		row.BeginIo(lease, Now);
		Should.Throw<BusinessException>(() => row.RecordOutcome(Guid.NewGuid(), Now, NotificationDispatchState.ProviderAccepted, "provider-id"));
		row.RecordOutcome(lease, Now, NotificationDispatchState.ProviderAccepted, "provider-id");
		row.Status.ShouldBe(NotificationDispatchState.ProviderAccepted); row.ProviderMessageId.ShouldBe("provider-id"); row.LeaseId.ShouldBeNull();
	}

	[Fact]
	public void Recovery_before_io_is_retryable_and_invalidates_the_old_claim()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue();
		row.Status.ShouldBe(NotificationDispatchState.Pending); row.LeaseId.ShouldBeNull(); row.IoStartedAt.ShouldBeNull();
		Should.Throw<BusinessException>(() => row.BeginIo(lease, Now.AddMinutes(6)));
	}

	[Fact]
	public void Recovery_after_io_never_blindly_retries_and_can_record_the_original_late_receipt()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing); row.BeginIo(lease, Now);
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue();
		row.Status.ShouldBe(NotificationDispatchState.UnknownAcceptance); row.IoLeaseId.ShouldBe(lease);
		Should.Throw<BusinessException>(() => row.ScheduleRetry(lease, Now.AddMinutes(6), Now.AddMinutes(7), "Timeout"));
		row.RecordOutcome(lease, Now.AddMinutes(6), NotificationDispatchState.ProviderAccepted, "late-receipt");
		row.Status.ShouldBe(NotificationDispatchState.ProviderAccepted); row.ProviderMessageId.ShouldBe("late-receipt");
	}

	[Fact]
	public void Only_confirmed_nonacceptance_can_be_scheduled_for_retry()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing); row.BeginIo(lease, Now);
		row.RecordOutcome(lease, Now, NotificationDispatchState.Failed, errorCode: "ProviderBusy");
		row.ScheduleRetry(lease, Now, Now.AddMinutes(1), "ProviderBusy");
		row.Status.ShouldBe(NotificationDispatchState.Pending); row.NextAttemptAt.ShouldBe(Now.AddMinutes(1));
		row.IoStartedAt.ShouldBeNull(); row.IoLeaseId.ShouldBeNull();
		Should.Throw<BusinessException>(() => row.RecordOutcome(lease, Now, NotificationDispatchState.ProviderAccepted, "stale"));
	}

	[Fact]
	public void An_expired_message_without_io_is_skipped_instead_of_requeued()
	{
		var row = Dispatch(Now.AddMinutes(4)); HydrateClaim(row, NotificationDispatchState.Processing);
		row.RecoverExpiredLease(Now.AddMinutes(6)).ShouldBeTrue();
		row.Status.ShouldBe(NotificationDispatchState.Skipped); row.ErrorCode.ShouldBe("SourceExpired");
	}

	[Fact]
	public void Recovery_does_not_steal_an_active_lease()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		row.RecoverExpiredLease(Now.AddMinutes(1)).ShouldBeFalse(); row.LeaseId.ShouldBe(lease);
	}

	[Fact]
	public void Pre_io_revocation_skips_without_creating_a_false_provider_receipt()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		row.SkipBeforeIo(lease, Now, "ConsentWithdrawn");
		row.Status.ShouldBe(NotificationDispatchState.Skipped); row.ErrorCode.ShouldBe("ConsentWithdrawn");
		row.ProviderMessageId.ShouldBeNull(); row.IoStartedAt.ShouldBeNull(); row.LeaseId.ShouldBeNull();
	}

	[Fact]
	public void General_error_fields_do_not_accept_raw_addresses_or_message_bodies()
	{
		var row = Dispatch(); var lease = HydrateClaim(row, NotificationDispatchState.Processing);
		Should.Throw<BusinessException>(() => row.SkipBeforeIo(lease, Now, "guardian@example.test"));
		row.Status.ShouldBe(NotificationDispatchState.Processing);
	}

	private static NotificationIntent Intent(Guid? actor = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2,
		"Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(), "semantic-1", Guid.NewGuid().ToString("N"),
		Now, Now, "[]", "[]", actorUserId: actor);
	private static NotificationDispatch Dispatch(DateTimeOffset? expiresAt = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		"{}", "user", CommunicationChannelType.Email, "fingerprint", "protected", "hash", Guid.NewGuid().ToString("N"), Now, expiresAt);

	// Rehydrate the same private lease fields written by the PostgreSQL claim query.
	private static Guid HydrateClaim<T>(T row, object state)
	{
		var id = Guid.NewGuid();
		typeof(T).GetProperty("Status")!.SetValue(row, state);
		typeof(T).GetProperty("LeaseId")!.SetValue(row, id);
		typeof(T).GetProperty("ClaimedAt")!.SetValue(row, Now);
		typeof(T).GetProperty("LeaseExpiresAt")!.SetValue(row, Now.AddMinutes(5));
		return id;
	}
}
