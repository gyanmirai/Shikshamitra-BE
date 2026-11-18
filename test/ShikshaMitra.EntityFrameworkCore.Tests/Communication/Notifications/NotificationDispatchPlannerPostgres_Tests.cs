using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDispatchPlannerPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	private const string EventKey = "Academic.Homework.Published";
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private sealed record Work(Guid TenantId, Guid CampaignId, Guid UserId, NotificationRecipientRefDto Student, NotificationQueueClaim Claim);
	private sealed record Runtime(ICurrentTenant Tenant, IClock Clock, NotificationIntentSourceReader Sources,
		INotificationRuntimeRecipientResolver Recipients, NotificationDispatchPolicy Policy, NotificationEmailRoute Email,
		NotificationSourceLink Links, NotificationTemplateRenderer Renderer, NotificationDispatchPayloadProtector Protector,
		IAsyncQueryableExecuter Queries, IGuidGenerator Guids, WhatsAppProviderRoute WhatsApp, WhatsAppApprovedTemplates Approvals);

	[NotificationPostgresFact]
	public async Task Rolling_back_planning_reverts_evidence_dispatch_and_completion_under_the_same_lease()
	{
		var work = await SeedAndClaimAsync();
		await using (var first = fixture.CreateContext())
		{
			await using var transaction = await first.Database.BeginTransactionAsync();
			await CreatePlanner(first, work).PlanAsync(work.Claim, default);
			(await first.Set<NotificationDispatch>().CountAsync(x => x.TenantId == work.TenantId)).ShouldBe(1);
			(await first.Set<CommunicationRecipientSnapshot>().CountAsync(x => x.TenantId == work.TenantId)).ShouldBe(1);
			await transaction.RollbackAsync();
		}
		await using (var retry = fixture.CreateContext())
		{
			var before = await retry.Set<NotificationIntent>().AsNoTracking().SingleAsync(x => x.Id == work.Claim.Id);
			before.Status.ShouldBe(NotificationIntentState.Processing);
			before.LeaseId.ShouldBe(work.Claim.LeaseId);
			(await retry.Set<NotificationDispatch>().CountAsync(x => x.TenantId == work.TenantId)).ShouldBe(0);
			(await retry.Set<CommunicationRecipientSnapshot>().CountAsync(x => x.TenantId == work.TenantId)).ShouldBe(0);
			await using var transaction = await retry.Database.BeginTransactionAsync();
			await CreatePlanner(retry, work).PlanAsync(work.Claim, default);
			await transaction.CommitAsync();
		}
		await AssertCommittedAsync(work);
	}

	[NotificationPostgresFact]
	public async Task Dispatcher_commits_the_io_boundary_before_the_adapter_and_saves_its_receipt_separately()
	{
		var work = await SeedAndClaimAsync();
		var keys = new EphemeralDataProtectionProvider();
		await using (var planning = fixture.CreateContext())
		{
			await using var transaction = await planning.Database.BeginTransactionAsync();
			await CreatePlanner(planning, work, keys: keys).PlanAsync(work.Claim, default);
			await transaction.CommitAsync();
		}
		await using var connection = fixture.CreateConnection();
		var claimed = (await NotificationPostgresQueueClaims.ClaimAsync(connection, null, work.TenantId,
			NotificationQueueKind.Dispatch, Now, 1, TimeSpan.FromMinutes(5), default)).ShouldHaveSingleItem();
		await using var db = fixture.CreateContext();
		var runtime = CreateRuntime(db, work, keys);
		var units = Substitute.For<IUnitOfWorkManager>();
		var starts = 0;
		var commits = 0;
		IUnitOfWork? active = null;
		units.Current.Returns(_ => active);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => {
			active.ShouldBeNull();
			db.ChangeTracker.Clear();
			var transaction = db.Database.BeginTransaction();
			var unit = Substitute.For<IUnitOfWork>();
			starts++;
			active = unit;
			unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(async call => {
				await db.SaveChangesAsync(call.Arg<CancellationToken>());
				await transaction.CommitAsync(call.Arg<CancellationToken>());
				commits++;
			});
			unit.When(x => x.Dispose()).Do(_ => { transaction.Dispose(); active = null; });
			return unit;
		});
		var sender = Substitute.For<INotificationChannelSender>();
		sender.Channel.Returns(CommunicationChannelType.InApp);
		sender.SendAsync(Arg.Any<NotificationChannelSendRequest>(), Arg.Any<CancellationToken>()).Returns(async _ => {
			active.ShouldBeNull();
			commits.ShouldBe(1);
			await using var observer = fixture.CreateContext();
			var observed = await observer.Set<NotificationDispatch>().AsNoTracking().SingleAsync(x => x.Id == claimed.Id);
			observed.IoStartedAt.ShouldBe(Now);
			observed.IoLeaseId.ShouldBe(claimed.LeaseId);
			observed.Status.ShouldBe(NotificationDispatchState.Processing);
			return new NotificationChannelSendResult(NotificationProviderAcceptance.Accepted, "inapp:receipt", null);
		});
		var locks = Substitute.For<INotificationPersonalWriteLock>();
		var dispatcher = new NotificationDispatcher(runtime.Tenant, units, runtime.Clock,
			new(runtime.Protector, runtime.Sources, [runtime.Recipients], runtime.Policy, runtime.Email, runtime.Links,
				runtime.WhatsApp, runtime.Approvals), [sender],
			new(locks), Repository<NotificationDispatch>(db), Repository<NotificationIntent>(db), Repository<CommunicationCampaign>(db),
			runtime.Queries, CreateDeliveryProjection(db, runtime, locks));

		await dispatcher.DispatchAsync(claimed.Id, claimed.LeaseId, default);

		starts.ShouldBe(2);
		commits.ShouldBe(2);
		active.ShouldBeNull();
		await using var final = fixture.CreateContext();
		var delivered = await final.Set<NotificationDispatch>().SingleAsync(x => x.Id == claimed.Id);
		delivered.Status.ShouldBe(NotificationDispatchState.Delivered);
		delivered.ProviderMessageId.ShouldBe("inapp:receipt");
		delivered.AttemptNumber.ShouldBe(1);
		delivered.LeaseId.ShouldBeNull();
		var attempt = await final.Set<DeliveryAttempt>().SingleAsync(x => x.NotificationDispatchId == delivered.Id);
		attempt.Status.ShouldBe(CommunicationDeliveryStatus.Delivered);
		attempt.DeliveredAt.ShouldBe(Now);
	}

	[NotificationPostgresFact]
	public async Task Two_transactions_with_the_same_lease_cannot_commit_duplicate_dispatches_or_history()
	{
		var work = await SeedAndClaimAsync();
		await using var first = fixture.CreateContext();
		await using var contender = fixture.CreateContext();
		await using var firstTransaction = await first.Database.BeginTransactionAsync();
		await using var contenderTransaction = await contender.Database.BeginTransactionAsync();
		await CreatePlanner(first, work).PlanAsync(work.Claim, default);
		var loadedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var second = CreatePlanner(contender, work, loadedSource).PlanAsync(work.Claim, default);
		await loadedSource.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await firstTransaction.CommitAsync();
		await Should.ThrowAsync<DbUpdateException>(() => second.WaitAsync(TimeSpan.FromSeconds(10)));
		await contenderTransaction.RollbackAsync();
		await AssertCommittedAsync(work);
	}

	[NotificationPostgresFact]
	public async Task Inbox_delivery_rolls_back_with_its_receipt_and_competing_senders_reuse_one_committed_item()
	{
		var work = await SeedAndClaimAsync();
		var keys = new EphemeralDataProtectionProvider();
		await using (var planning = fixture.CreateContext())
		{
			await using var transaction = await planning.Database.BeginTransactionAsync();
			await CreatePlanner(planning, work, keys: keys).PlanAsync(work.Claim, default);
			await transaction.CommitAsync();
		}
		await using var connection = fixture.CreateConnection();
		var claim = (await NotificationPostgresQueueClaims.ClaimAsync(connection, null, work.TenantId, NotificationQueueKind.Dispatch,
			Now, 1, TimeSpan.FromMinutes(5), default)).ShouldHaveSingleItem();
		NotificationChannelSendRequest request;
		await using (var preparation = fixture.CreateContext())
		{
			var row = await preparation.Set<NotificationDispatch>().SingleAsync(x => x.Id == claim.Id);
			row.BeginIo(claim.LeaseId, Now);
			await preparation.SaveChangesAsync();
			request = new(row.Id, row.DeduplicationKey, Guid.Empty, row.Channel, work.UserId.ToString("N"), "Homework",
				"Update for Secret Learner", "https://school.example.invalid/portal/homework", "en", row.AttemptNumber,
				TemplateRevisionId: row.TemplateRevisionId, Recipient: new("Guardian", work.Student.StudentId, "mother", work.UserId));
		}
		await using (var interrupted = fixture.CreateContext())
		{
			var sender = CreateInboxSender(interrupted, work, keys, failBeforeCommit: true);
			await Should.ThrowAsync<DbUpdateException>(() => sender.SendAsync(request, default));
		}
		await using (var afterRollback = fixture.CreateContext())
		{
			(await afterRollback.Set<NotificationInboxItem>().CountAsync(x => x.DispatchId == claim.Id)).ShouldBe(0);
			(await afterRollback.Set<NotificationDispatch>().SingleAsync(x => x.Id == claim.Id)).Status.ShouldBe(NotificationDispatchState.Processing);
		}
		await using var first = fixture.CreateContext();
		await using var second = fixture.CreateContext();
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var results = await Task.WhenAll(CreateInboxSender(first, work, keys).SendAsync(request, timeout.Token),
			CreateInboxSender(second, work, keys).SendAsync(request, timeout.Token));

		results[0].Acceptance.ShouldBe(NotificationProviderAcceptance.Accepted);
		results[1].ShouldBe(results[0]);
		await using var check = fixture.CreateContext();
		var item = await check.Set<NotificationInboxItem>().SingleAsync(x => x.DispatchId == claim.Id);
		item.StudentId.ShouldBe(work.Student.StudentId);
		item.UserId.ShouldBe(work.UserId);
		var delivered = await check.Set<NotificationDispatch>().SingleAsync(x => x.Id == claim.Id);
		delivered.Status.ShouldBe(NotificationDispatchState.Delivered);
		delivered.ProviderMessageId.ShouldBe($"inbox:{item.Id:N}");
	}

	private static InAppNotificationChannelSender CreateInboxSender(NotificationPersistenceTestDbContext db, Work work,
		IDataProtectionProvider keys, bool failBeforeCommit = false)
	{
		var runtime = CreateRuntime(db, work, keys);
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns((IUnitOfWork?)null);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(_ => {
			var transaction = db.Database.BeginTransaction();
			var unit = Substitute.For<IUnitOfWork>();
			unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(async call => {
				var token = call.Arg<CancellationToken>();
				await db.SaveChangesAsync(token);
				if (failBeforeCommit) throw new DbUpdateException("Simulated failure before the inbox transaction commits.");
				await transaction.CommitAsync(token);
			});
			unit.When(x => x.Dispose()).Do(_ => transaction.Dispose());
			return unit;
		});
		var locks = Substitute.For<INotificationPersonalWriteLock>();
		locks.AcquireAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(call =>
			NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction(),
				work.TenantId, call.Arg<IReadOnlyList<string>>(), call.Arg<CancellationToken>()));
		var user = new IdentityUser(work.UserId, "test-account", "test@example.invalid", work.TenantId);
		user.SetIsActive(true);
		var users = Substitute.For<IRepository<IdentityUser, Guid>>();
		users.GetQueryableAsync().Returns(new[] { user }.AsQueryable());
		var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example.invalid"
		}).Build();
		return new(runtime.Tenant, units, runtime.Clock, new(locks), Repository<NotificationDispatch>(db), Repository<NotificationIntent>(db),
			Repository<NotificationInboxItem>(db), users, runtime.Queries, runtime.Guids, new([], config),
			CreateDeliveryProjection(db, runtime, locks));
	}

	private static NotificationDeliveryProjection CreateDeliveryProjection(DbContext db, Runtime runtime, INotificationPersonalWriteLock locks) =>
		new(runtime.Tenant, locks, new(runtime.Tenant, Substitute.For<ISettingManager>()), Repository<ChannelProviderSetting>(db),
			Repository<DeliveryAttempt>(db), runtime.Queries, runtime.Guids, runtime.Clock);

	private async Task AssertCommittedAsync(Work work)
	{
		await using var check = fixture.CreateContext();
		var intent = await check.Set<NotificationIntent>().AsNoTracking().SingleAsync(x => x.Id == work.Claim.Id);
		intent.Status.ShouldBe(NotificationIntentState.AwaitingDelivery);
		intent.LeaseId.ShouldBeNull();
		var dispatch = await check.Set<NotificationDispatch>().SingleAsync(x => x.IntentId == intent.Id);
		var evidence = await check.Set<CommunicationRecipientSnapshot>().SingleAsync(x => x.CampaignId == work.CampaignId);
		dispatch.RecipientSnapshotId.ShouldBe(evidence.Id);
		dispatch.ProtectedMessageJson.ShouldNotContain("Secret Learner");
		evidence.ChannelEligibilityJson.ShouldNotContain("Secret Learner");
		evidence.IsEligible.ShouldBeTrue();
		await using var transaction = await check.Database.BeginTransactionAsync();
		var error = await Should.ThrowAsync<BusinessException>(() => CreatePlanner(check, work).PlanAsync(work.Claim, default));
		error.Code.ShouldBe(NotificationErrorCodes.LeaseLost);
		await transaction.RollbackAsync();
	}

	private async Task<Work> SeedAndClaimAsync()
	{
		var tenantId = Guid.NewGuid();
		var owner = Guid.NewGuid();
		var student = new NotificationRecipientRefDto("Student", Guid.NewGuid());
		var templateId = Guid.NewGuid();
		var sourceId = Guid.NewGuid();
		var definition = new AcademicNotificationCatalogue([]).Get(EventKey);
		var campaign = new CommunicationCampaign(Guid.NewGuid(), tenantId) {
			CampaignType = NotificationCampaignTypes.Academic, SourceEntityId = sourceId, SourceEntityType = definition.SourceType
		};
		var template = new CommunicationTemplate(templateId, tenantId) { TemplateName = "Homework", Body = "Homework" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenantId, templateId, 1, EventKey, "Guardian",
			CommunicationChannelType.InApp, "en", "Homework", "Update for {{student_name}}", 1);
		revision.Publish(owner, Now);
		var rule = new NotificationRule(Guid.NewGuid(), tenantId, EventKey, NotificationRuleScope.Create());
		var configuration = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp], [templateId], NotificationRuleTimingConfiguration.Default, "en");
		var codec = new NotificationRuleConfigurationCodec();
		rule.Configure(true, codec.Serialize(configuration, definition), rule.ConcurrencyStamp);
		var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
		var intent = new NotificationIntent(Guid.NewGuid(), tenantId, rule.Id, rule.Revision, EventKey, definition.SourceType,
			sourceId, "v1", "test", Now.AddMinutes(-1), Now.AddMinutes(-1), JsonSerializer.Serialize(new[] { student }, json),
			JsonSerializer.Serialize(new[] { revision.Id }, json));
		intent.BindRuntimeSnapshot(new NotificationIntentSnapshotCodec(codec).Serialize(new(1, new(), configuration), definition),
			NotificationIntentOrigin.Automatic);
		intent.BindAutomaticOperation(campaign.Id);
		await using (var seed = fixture.CreateContext())
		{
			seed.AddRange(campaign, template, revision, rule, intent);
			await seed.SaveChangesAsync();
		}
		await using var connection = fixture.CreateConnection();
		var claims = await NotificationPostgresQueueClaims.ClaimAsync(connection, null, tenantId, NotificationQueueKind.Intent,
			Now, 1, TimeSpan.FromMinutes(5), default);
		return new(tenantId, campaign.Id, owner, student, claims.ShouldHaveSingleItem());
	}

	private static NotificationDispatchPlanner CreatePlanner(NotificationPersistenceTestDbContext db, Work work,
		TaskCompletionSource? loadedSource = null, IDataProtectionProvider? keys = null)
	{
		var runtime = CreateRuntime(db, work, keys ?? new EphemeralDataProtectionProvider(), loadedSource);
		var unit = Substitute.For<IUnitOfWork>();
		unit.Options.Returns(new AbpUnitOfWorkOptions { IsTransactional = true });
		unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => db.SaveChangesAsync(call.Arg<CancellationToken>()));
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns(unit);
		return new(runtime.Tenant, units, runtime.Clock, runtime.Sources, [runtime.Recipients], runtime.Policy, runtime.Email,
			new(runtime.Tenant, Repository<CommunicationTemplateRevision>(db), runtime.Queries, runtime.Renderer, runtime.Links,
				runtime.Protector, runtime.Guids, runtime.WhatsApp, runtime.Approvals),
			Repository<NotificationIntent>(db), Repository<CommunicationCampaign>(db), Repository<NotificationDispatch>(db),
			Repository<CommunicationRecipientSnapshot>(db), runtime.Queries, runtime.Guids);
	}

	private static Runtime CreateRuntime(NotificationPersistenceTestDbContext db, Work work, IDataProtectionProvider keys,
		TaskCompletionSource? loadedSource = null)
	{
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(work.TenantId);
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(Now.UtcDateTime);
		var adapter = Substitute.For<INotificationSourceAdapter>();
		adapter.EventKeys.Returns(new[] { EventKey });
		adapter.LoadAsync(Arg.Any<NotificationIntentContext>(), Arg.Any<CancellationToken>()).Returns(_ => {
			loadedSource?.TrySetResult();
			return new NotificationSourceSnapshot(true, null, "v1", "/portal/homework", new Dictionary<string, NotificationVariableValueDto> {
				["school_name"] = new(NotificationVariableType.Text, "School"),
			}, [work.Student], Now.AddHours(1), [new(new(), [work.Student])]);
		});
		var resolver = Substitute.For<INotificationRuntimeRecipientResolver>();
		resolver.Supports(EventKey).Returns(true);
		resolver.ResolveAsync(Arg.Any<NotificationCurrentSource>(), Arg.Any<CancellationToken>()).Returns(
			Task.FromResult<IReadOnlyList<NotificationRuntimeRecipient>>([
				new(work.Student, new("Guardian", work.Student.StudentId, "mother", work.UserId), CommunicationChannelType.InApp,
					null, "association-revision", recipientName: "Test Guardian", studentName: "Secret Learner")
			]));
		var catalogue = new AcademicNotificationCatalogue([adapter]);
		var queries = new AsyncQueryableExecuter([]);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", work.TenantId.ToString(), false).Returns("UTC");
		var app = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example.invalid"
		}).Build();
		var validator = new NotificationTemplateVariableValidator([catalogue], app);
		var revisions = Repository<CommunicationTemplateRevision>(db);
		var renderer = new NotificationTemplateRenderer(tenant, revisions, queries, validator);
		var ids = Substitute.For<IGuidGenerator>();
		ids.Create().Returns(_ => Guid.NewGuid());
		return new(tenant, clock, new(tenant, [catalogue], [adapter], new(new()), new()), resolver,
			new(tenant, new(tenant, Repository<NotificationRule>(db), queries), Repository<NotificationPreference>(db),
				Repository<ConsentRecord>(db), queries, new(tenant, settings), new()),
			new(tenant, Repository<ChannelProviderSetting>(db), queries),
			new(app, validator), renderer, new(keys, tenant), queries, ids,
			new(tenant, Repository<ChannelProviderSetting>(db), queries, app),
			new(tenant, Repository<WhatsAppTemplateBinding>(db), queries));
	}

	private static IRepository<T, Guid> Repository<T>(DbContext db) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(db.Set<T>());
		repository.InsertAsync(Arg.Any<T>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<T>();
			db.Add(row);
			return row;
		});
		return repository;
	}
}
