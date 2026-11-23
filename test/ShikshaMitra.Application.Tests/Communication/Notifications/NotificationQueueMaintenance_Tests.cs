using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueueMaintenance_Tests
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IUnitOfWorkManager _units = Substitute.For<IUnitOfWorkManager>();
	private readonly IUnitOfWork _unit = Substitute.For<IUnitOfWork>();
	private readonly INotificationPersonalWriteLock _writeLock = Substitute.For<INotificationPersonalWriteLock>();
	private readonly List<NotificationIntent> _intents = [];
	private readonly List<NotificationDispatch> _dispatches = [];
	private readonly List<DeliveryAttempt> _attempts = [];
	private readonly List<CommunicationCampaign> _campaigns = [];
	private readonly NotificationLeaseRecoveryService _recovery;
	private readonly NotificationOutcomeProjector _projector;

	public NotificationQueueMaintenance_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_unit);
		var queries = new AsyncQueryableExecuter([]);
		var intents = Repository(_intents);
		var dispatches = Repository(_dispatches);
		var delivery = new NotificationDeliveryProjection(_tenant, _writeLock, new(_tenant, Substitute.For<Volo.Abp.SettingManagement.ISettingManager>()),
			Repository<ChannelProviderSetting>([]), Repository(_attempts), queries, Substitute.For<Volo.Abp.Guids.IGuidGenerator>(),
			Substitute.For<Volo.Abp.Timing.IClock>());
		_recovery = new(_tenant, _units, intents, dispatches, queries, delivery);
		_projector = new(_tenant, _units, new(_writeLock), intents, dispatches, Repository(_campaigns), queries);
	}

	[Fact]
	public async Task Recovery_covers_stale_leases_and_expired_unclaimed_work_without_crossing_school_boundaries()
	{
		var overdue = Intent(Operation(), expiresAt: Now);
		var lost = Intent(Operation());
		HydrateIntentLease(lost, Now.AddSeconds(-1));
		var active = Intent(Operation());
		HydrateIntentLease(active, Now.AddSeconds(1));
		var queued = Dispatch(lost, expiresAt: Now);
		var beforeIo = Dispatch(lost);
		HydrateDispatchLease(beforeIo, expired: true, startedIo: false);
		var afterIo = Dispatch(lost, expiresAt: Now.AddMinutes(-1));
		var ioLease = HydrateDispatchLease(afterIo, expired: true, startedIo: true);
		var reserved = new DeliveryAttempt(Guid.NewGuid(), _tenantId) {
			NotificationDispatchId = afterIo.Id, AttemptNumber = afterIo.AttemptNumber, QueuedAt = Now.AddMinutes(-3)
		};
		_attempts.Add(reserved);
		Set(afterIo, nameof(NotificationDispatch.DeliveryAttemptId), reserved.Id);
		var foreign = Dispatch(Intent(Operation(Guid.NewGuid())), expiresAt: Now.AddMinutes(-2));

		var result = await _recovery.RecoverAsync(Now, 10, default);

		result.Intents.ShouldBe(2);
		result.Dispatches.ShouldBe(3);
		result.IntentLeases.ShouldBe(1);
		result.DispatchLeases.ShouldBe(2);
		reserved.Status.ShouldBe(CommunicationDeliveryStatus.UnknownAcceptance);
		overdue.Status.ShouldBe(NotificationIntentState.Expired);
		lost.Status.ShouldBe(NotificationIntentState.Ready);
		active.Status.ShouldBe(NotificationIntentState.Processing);
		queued.Status.ShouldBe(NotificationDispatchState.Skipped);
		queued.ErrorCode.ShouldBe("SourceExpired");
		beforeIo.Status.ShouldBe(NotificationDispatchState.Pending);
		beforeIo.AttemptNumber.ShouldBe(0);
		afterIo.Status.ShouldBe(NotificationDispatchState.UnknownAcceptance);
		afterIo.IoLeaseId.ShouldBe(ioLease);
		foreign.Status.ShouldBe(NotificationDispatchState.Pending);
		await _unit.Received(1).CompleteAsync(default);
	}

	[Fact]
	public async Task Recovery_limits_each_queue_and_processes_earliest_expiry_first()
	{
		var later = Intent(Operation(), expiresAt: Now);
		var earlier = Intent(Operation(), expiresAt: Now.AddMinutes(-1));
		var result = await _recovery.RecoverAsync(Now, 1, default);
		result.Intents.ShouldBe(1);
		result.IntentLeases.ShouldBe(0);
		earlier.Status.ShouldBe(NotificationIntentState.Expired);
		later.Status.ShouldBe(NotificationIntentState.Ready);
	}

	[Fact]
	public async Task Projection_preserves_actual_delivery_counts_and_keeps_active_siblings_pending()
	{
		var operation = Operation();
		var settled = Intent(operation, planned: true);
		var active = Intent(operation, planned: true);
		Finish(Dispatch(settled), NotificationDispatchState.ProviderAccepted);
		Finish(Dispatch(settled), NotificationDispatchState.Delivered);
		Dispatch(active);

		(await _projector.RefreshBatchAsync(20, default)).ShouldBe(1);

		settled.Status.ShouldBe(NotificationIntentState.Completed);
		active.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
		operation.Status.ShouldBe(CommunicationCampaignStatus.Queued);
		operation.DeliveredCount.ShouldBe(1);
		operation.QueuedCount.ShouldBe(1);
		operation.FailedCount.ShouldBe(0);
		await _writeLock.Received(1).AcquireAsync(Arg.Is<IReadOnlyList<string>>(x =>
			x.Count == 1 && x[0] == $"notification-outcome:{operation.Id:N}"), default);
	}

	[Fact]
	public async Task Unknown_delivery_requires_review_and_later_reopened_evidence_updates_the_same_operation()
	{
		var operation = Operation();
		var intent = Intent(operation, planned: true);
		var row = Dispatch(intent);
		var lease = Finish(row, NotificationDispatchState.UnknownAcceptance);
		await _projector.RefreshBatchAsync(20, default);
		intent.Status.ShouldBe(NotificationIntentState.NeedsReview);
		intent.ErrorCode.ShouldBe(NotificationErrorCodes.UnknownAcceptance);
		operation.Status.ShouldBe(CommunicationCampaignStatus.Failed);
		operation.FailedCount.ShouldBe(0);

		row.RecordOutcome(lease, Now, NotificationDispatchState.ProviderAccepted, "late-id");
		intent.ReopenDeliveryProjection();
		await _projector.RefreshBatchAsync(20, default);

		intent.Status.ShouldBe(NotificationIntentState.Completed);
		operation.Status.ShouldBe(CommunicationCampaignStatus.Sent);
		operation.DeliveredCount.ShouldBe(0);
		(await _projector.RefreshBatchAsync(20, default)).ShouldBe(0);
	}

	[Fact]
	public async Task Terminal_dispatches_preserve_partial_failure_and_never_touch_generic_or_foreign_operations()
	{
		var owned = Operation();
		var done = Intent(owned, planned: true);
		Finish(Dispatch(done), NotificationDispatchState.Delivered);
		Finish(Dispatch(done), NotificationDispatchState.Failed);
		var generic = Operation(type: "Announcement");
		var unrelated = Intent(generic, planned: true);
		var foreign = Intent(Operation(Guid.NewGuid()), planned: true);

		(await _projector.RefreshBatchAsync(20, default)).ShouldBe(1);

		done.Status.ShouldBe(NotificationIntentState.PartiallyFailed);
		owned.Status.ShouldBe(CommunicationCampaignStatus.PartiallyFailed);
		owned.DeliveredCount.ShouldBe(1);
		owned.FailedCount.ShouldBe(1);
		unrelated.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
		foreign.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
	}

	[Fact]
	public async Task Expired_capture_without_dispatches_closes_the_queued_operation()
	{
		var operation = Operation();
		var expired = Intent(operation, expiresAt: Now);
		expired.StopBeforePlanning(NotificationIntentState.Expired, "SourceExpired");
		(await _projector.RefreshBatchAsync(20, default)).ShouldBe(1);
		operation.Status.ShouldBe(CommunicationCampaignStatus.Cancelled);
		expired.Status.ShouldBe(NotificationIntentState.Expired);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Pilot_projection_repairs_only_selected_delivery_or_pre_io_skip(bool delivered)
	{
		var selected = Operation();
		selected.SourceEntityType = NotificationCampaignTypes.AcademicTemplateTestSource;
		var recipient = Guid.NewGuid();
		var intent = NotificationIntent.CreateTemplateTest(Guid.NewGuid(), _tenantId, "Academic.Homework.Published",
			selected.SourceEntityId!.Value, "hash", "[]", "[]", Now, Now.AddMinutes(10), recipient, selected.Id);
		var reference = NotificationRuntimeJson.Serialize(new NotificationRecipientRefDto("Guardian", UserId: recipient),
			NotificationErrorCodes.RuntimeSnapshotInvalid);
		var row = new NotificationDispatch(Guid.NewGuid(), _tenantId, intent.Id, Guid.NewGuid(), reference, "context",
			CommunicationChannelType.InApp, "fingerprint", "cipher", "hash", "key", Now, Now.AddMinutes(10));
		row.BindPlanningEvidence(selected.Id, Guid.NewGuid(), null);
		_intents.Add(intent);
		_dispatches.Add(row);
		var other = Operation();
		Finish(Dispatch(Intent(other, planned: true)), NotificationDispatchState.Delivered);
		if (delivered)
			Finish(row, NotificationDispatchState.Delivered);
		else
			row.SkipBeforeIo(HydrateDispatchLease(row, expired: false, startedIo: false), Now, NotificationErrorCodes.RecipientIneligible);
		row.AttemptNumber.ShouldBe(delivered ? 1 : 0);
		var pilot = new NotificationInAppPilot(_tenantId, selected.Id, recipient);
		await _projector.RefreshInAppPilotAsync(pilot with { RecipientUserId = Guid.NewGuid() }, default);
		selected.Status.ShouldBe(CommunicationCampaignStatus.Queued);

		await _projector.RefreshInAppPilotAsync(pilot, default);

		selected.Status.ShouldBe(delivered ? CommunicationCampaignStatus.Sent : CommunicationCampaignStatus.Cancelled);
		other.Status.ShouldBe(CommunicationCampaignStatus.Queued);
	}

	private CommunicationCampaign Operation(Guid? tenantId = null, string type = NotificationCampaignTypes.Academic)
	{
		var campaign = new CommunicationCampaign(Guid.NewGuid(), tenantId ?? _tenantId) {
			CampaignType = type, Status = CommunicationCampaignStatus.Queued, SourceEntityId = Guid.NewGuid(), SourceEntityType = "Academic.Homework"
		};
		_campaigns.Add(campaign);
		return campaign;
	}

	private NotificationIntent Intent(CommunicationCampaign campaign, bool planned = false, DateTimeOffset? expiresAt = null)
	{
		var row = new NotificationIntent(Guid.NewGuid(), campaign.TenantId!.Value, Guid.NewGuid(), 1, "Academic.Homework.Published",
			campaign.SourceEntityType!, campaign.SourceEntityId!.Value, "v1", "test", Now.AddHours(-1), Now.AddHours(-1), "[]", "[]", expiresAt);
		row.BindRuntimeSnapshot("""{"schemaVersion":1}""", NotificationIntentOrigin.Automatic);
		row.BindAutomaticOperation(campaign.Id);
		if (planned) row.CompletePlanning(HydrateIntentLease(row, Now.AddMinutes(5)), Now);
		_intents.Add(row);
		return row;
	}

	private NotificationDispatch Dispatch(NotificationIntent intent, DateTimeOffset? expiresAt = null)
	{
		var row = new NotificationDispatch(Guid.NewGuid(), intent.TenantId!.Value, intent.Id, Guid.NewGuid(), "{}", "context",
			CommunicationChannelType.Email, "fingerprint", "cipher", "hash", "key", Now, expiresAt);
		row.BindPlanningEvidence(intent.CampaignId!.Value, Guid.NewGuid(), null);
		_dispatches.Add(row);
		return row;
	}

	private static Guid HydrateIntentLease(NotificationIntent row, DateTimeOffset expiry)
	{
		var lease = Guid.NewGuid();
		Set(row, nameof(NotificationIntent.Status), NotificationIntentState.Processing);
		Set(row, nameof(NotificationIntent.LeaseId), lease);
		Set(row, nameof(NotificationIntent.LeaseExpiresAt), expiry);
		return lease;
	}

	private static Guid HydrateDispatchLease(NotificationDispatch row, bool expired, bool startedIo)
	{
		var lease = Guid.NewGuid();
		Set(row, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(row, nameof(NotificationDispatch.LeaseId), lease);
		Set(row, nameof(NotificationDispatch.LeaseExpiresAt), expired ? Now.AddMinutes(-1) : Now.AddMinutes(5));
		Set(row, nameof(NotificationDispatch.AttemptNumber), 1);
		if (startedIo) row.BeginIo(lease, expired ? Now.AddMinutes(-2) : Now);
		return lease;
	}

	private static Guid Finish(NotificationDispatch row, NotificationDispatchState state)
	{
		var lease = HydrateDispatchLease(row, expired: false, startedIo: true);
		row.RecordOutcome(lease, Now, state);
		return lease;
	}

	private static void Set<T>(T instance, string property, object value) => typeof(T).GetProperty(property)!.SetValue(instance, value);

	private static IRepository<T, Guid> Repository<T>(List<T> rows) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(_ => rows.AsQueryable());
		return repository;
	}
}
