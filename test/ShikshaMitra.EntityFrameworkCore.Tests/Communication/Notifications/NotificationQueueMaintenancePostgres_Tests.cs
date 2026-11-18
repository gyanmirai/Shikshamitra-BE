using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueueMaintenancePostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

	[NotificationPostgresFact]
	public async Task Concurrent_projectors_serialize_the_same_operation_and_refresh_actual_dispatch_counts_once()
	{
		var seed = await SeedAsync(queuedExpiry: false);
		await using var first = fixture.CreateContext();
		await using var contender = fixture.CreateContext();
		var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var contenderStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstProjector = Projector(first, seed.Tenant, afterLock: async () => {
			locked.TrySetResult();
			await resume.Task.WaitAsync(TimeSpan.FromSeconds(15));
		});
		var secondProjector = Projector(contender, seed.Tenant, beforeLock: () => contenderStarted.TrySetResult());
		var initial = firstProjector.RefreshBatchAsync(10, default);
		await (await Task.WhenAny(locked.Task, initial).WaitAsync(TimeSpan.FromSeconds(10)));
		locked.Task.IsCompletedSuccessfully.ShouldBeTrue();
		var overlapping = secondProjector.RefreshBatchAsync(10, default);
		await (await Task.WhenAny(contenderStarted.Task, overlapping).WaitAsync(TimeSpan.FromSeconds(10)));
		contenderStarted.Task.IsCompletedSuccessfully.ShouldBeTrue();
		(await Task.WhenAny(overlapping, Task.Delay(125))).ShouldNotBe(overlapping);
		resume.TrySetResult();
		(await initial.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(1);
		(await overlapping.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(0);

		await using var check = fixture.CreateContext();
		var operation = await check.Set<CommunicationCampaign>().SingleAsync(x => x.Id == seed.Operation);
		operation.Status.ShouldBe(CommunicationCampaignStatus.PartiallyFailed);
		operation.DeliveredCount.ShouldBe(1);
		operation.FailedCount.ShouldBe(0);
		operation.QueuedCount.ShouldBe(0);
		var intent = await check.Set<NotificationIntent>().SingleAsync(x => x.CampaignId == operation.Id);
		intent.Status.ShouldBe(NotificationIntentState.NeedsReview);
		intent.ErrorCode.ShouldBe(NotificationErrorCodes.UnknownAcceptance);
	}

	[NotificationPostgresFact]
	public async Task Expiry_sweep_uses_native_due_filters_then_projects_skipped_work_without_fake_acceptance()
	{
		var seed = await SeedAsync(queuedExpiry: true);
		await using var db = fixture.CreateContext();
		var tenant = Tenant(seed.Tenant);
		var recovery = new NotificationLeaseRecoveryService(tenant, Units(db), Repository<NotificationIntent>(db),
			Repository<NotificationDispatch>(db), new AsyncQueryableExecuter([]));
		var result = await recovery.RecoverAsync(Now, 1, default);
		result.Dispatches.ShouldBe(1);
		result.Intents.ShouldBe(0);
		(await Projector(db, seed.Tenant).RefreshBatchAsync(10, default)).ShouldBe(1);

		await using var check = fixture.CreateContext();
		var dispatch = await check.Set<NotificationDispatch>().SingleAsync(x => x.CampaignId == seed.Operation);
		dispatch.Status.ShouldBe(NotificationDispatchState.Skipped);
		dispatch.ErrorCode.ShouldBe("SourceExpired");
		dispatch.IoStartedAt.ShouldBeNull();
		dispatch.AttemptNumber.ShouldBe(0);
		var operation = await check.Set<CommunicationCampaign>().SingleAsync(x => x.Id == seed.Operation);
		operation.Status.ShouldBe(CommunicationCampaignStatus.Cancelled);
		operation.DeliveredCount.ShouldBe(0);
	}

	private async Task<(Guid Tenant, Guid Operation)> SeedAsync(bool queuedExpiry)
	{
		var tenant = Guid.NewGuid();
		var source = Guid.NewGuid();
		const string eventKey = "Academic.Homework.Published";
		const string sourceType = "Academic.Homework";
		var operation = new CommunicationCampaign(Guid.NewGuid(), tenant) {
			CampaignType = NotificationCampaignTypes.Academic, Status = CommunicationCampaignStatus.Queued,
			SourceEntityId = source, SourceEntityType = sourceType
		};
		var rule = new NotificationRule(Guid.NewGuid(), tenant, eventKey, NotificationRuleScope.Create());
		var intent = new NotificationIntent(Guid.NewGuid(), tenant, rule.Id, 1, eventKey, sourceType, source, "v1",
			"intent", Now.AddHours(-1), Now.AddHours(-1), "[]", "[]");
		intent.BindRuntimeSnapshot("""{"schemaVersion":1}""", NotificationIntentOrigin.Automatic);
		intent.BindAutomaticOperation(operation.Id);
		var planningLease = Guid.NewGuid();
		Set(intent, nameof(NotificationIntent.Status), NotificationIntentState.Processing);
		Set(intent, nameof(NotificationIntent.LeaseId), planningLease);
		Set(intent, nameof(NotificationIntent.LeaseExpiresAt), Now.AddMinutes(1));
		intent.CompletePlanning(planningLease, Now);
		var template = new CommunicationTemplate(Guid.NewGuid(), tenant) { TemplateName = "Test", Body = "Test" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, template.Id, 1, eventKey, "Guardian",
			CommunicationChannelType.InApp, "en", "Notice", "Body", 1);
		await using var seed = fixture.CreateContext();
		seed.AddRange(operation, rule, intent, template, revision);
		if (queuedExpiry) seed.Add(Dispatch("expired", Now));
		else
		{
			seed.Add(Finalize(Dispatch("delivered"), NotificationDispatchState.Delivered));
			seed.Add(Finalize(Dispatch("unknown"), NotificationDispatchState.UnknownAcceptance));
		}
		await seed.SaveChangesAsync();
		return (tenant, operation.Id);

		NotificationDispatch Dispatch(string key, DateTimeOffset? expiry = null)
		{
			var dispatch = new NotificationDispatch(Guid.NewGuid(), tenant, intent.Id, revision.Id, "{}", "context",
				CommunicationChannelType.InApp, "fingerprint", "encrypted", "hash", key, Now.AddHours(-1), expiry);
			dispatch.BindPlanningEvidence(operation.Id, Guid.NewGuid(), null);
			return dispatch;
		}
	}

	private static NotificationDispatch Finalize(NotificationDispatch row, NotificationDispatchState outcome)
	{
		var lease = Guid.NewGuid();
		Set(row, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
		Set(row, nameof(NotificationDispatch.LeaseId), lease);
		Set(row, nameof(NotificationDispatch.LeaseExpiresAt), Now.AddMinutes(5));
		Set(row, nameof(NotificationDispatch.AttemptNumber), 1);
		row.BeginIo(lease, Now);
		row.RecordOutcome(lease, Now, outcome);
		return row;
	}

	private static NotificationOutcomeProjector Projector(NotificationPersistenceTestDbContext db, Guid tenant,
		Action? beforeLock = null, Func<Task>? afterLock = null)
	{
		var writeLock = Substitute.For<INotificationPersonalWriteLock>();
		writeLock.AcquireAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(async call => {
			beforeLock?.Invoke();
			await NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction(),
				tenant, call.Arg<IReadOnlyList<string>>(), call.Arg<CancellationToken>());
			if (afterLock != null) await afterLock();
		});
		return new(Tenant(tenant), Units(db), new(writeLock), Repository<NotificationIntent>(db), Repository<NotificationDispatch>(db),
			Repository<CommunicationCampaign>(db), new AsyncQueryableExecuter([]));
	}

	private static ICurrentTenant Tenant(Guid tenant)
	{
		var current = Substitute.For<ICurrentTenant>();
		current.Id.Returns(tenant);
		return current;
	}

	private static IUnitOfWorkManager Units(NotificationPersistenceTestDbContext db)
	{
		var manager = Substitute.For<IUnitOfWorkManager>();
		manager.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(call => {
			db.ChangeTracker.Clear();
			var transaction = call.Arg<AbpUnitOfWorkOptions>().IsTransactional ? db.Database.BeginTransaction() : null;
			var unit = Substitute.For<IUnitOfWork>();
			unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(save => db.SaveChangesAsync(save.Arg<CancellationToken>()));
			unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(async complete => {
				if (transaction == null) return;
				var token = complete.Arg<CancellationToken>();
				await db.SaveChangesAsync(token);
				await transaction.CommitAsync(token);
			});
			unit.When(x => x.Dispose()).Do(_ => transaction?.Dispose());
			return unit;
		});
		return manager;
	}

	private static IRepository<T, Guid> Repository<T>(DbContext db) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(db.Set<T>());
		return repository;
	}

	private static void Set<T>(T instance, string property, object value) => typeof(T).GetProperty(property)!.SetValue(instance, value);
}
