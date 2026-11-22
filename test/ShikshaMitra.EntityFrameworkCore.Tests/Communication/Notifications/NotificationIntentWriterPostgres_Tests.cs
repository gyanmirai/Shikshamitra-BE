using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationIntentWriterPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	private static readonly DateTimeOffset Now = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
	private const string DueEvent = "Academic.Homework.Due";
	[NotificationPostgresFact]
	public async Task Actual_writer_rollback_reverts_the_flushed_source_and_all_notification_work()
	{
		var seed = await SeedAsync("draft");
		await using (var db = fixture.CreateContext())
		{
			await using var transaction = await db.Database.BeginTransactionAsync();
			(await db.Set<CommunicationTemplate>().SingleAsync(x => x.Id == seed.SourceId)).Body = "published";
			var harness = CreateWriter(db, seed);
			(await harness.Writer.WriteAsync(Input(seed), default)).ShouldNotBeNull();
			(await db.Set<NotificationIntent>().CountAsync(x => x.SourceId == seed.SourceId)).ShouldBe(1);
			await transaction.RollbackAsync();
		}
		await using var check = fixture.CreateContext();
		(await check.Set<CommunicationTemplate>().SingleAsync(x => x.Id == seed.SourceId)).Body.ShouldBe("draft");
		(await check.Set<NotificationIntent>().CountAsync(x => x.SourceId == seed.SourceId)).ShouldBe(0);
		(await check.Set<CommunicationCampaign>().CountAsync(x => x.SourceEntityId == seed.SourceId)).ShouldBe(0);
	}
	[NotificationPostgresFact]
	public async Task Native_persistence_failure_aborts_source_and_operation_instead_of_becoming_a_capture_issue()
	{
		var seed = await SeedAsync("draft");
		await using (var db = fixture.CreateContext())
		{
			await using var transaction = await db.Database.BeginTransactionAsync();
			(await db.Set<CommunicationTemplate>().SingleAsync(x => x.Id == seed.SourceId)).Body = "published";
			var harness = CreateWriter(db, seed, breakIntentTenant: true);
			await Should.ThrowAsync<DbUpdateException>(() => harness.Writer.WriteAsync(Input(seed), default));
			await transaction.RollbackAsync();
		}
		await using var check = fixture.CreateContext();
		(await check.Set<CommunicationTemplate>().SingleAsync(x => x.Id == seed.SourceId)).Body.ShouldBe("draft");
		(await check.Set<CommunicationCampaign>().CountAsync(x => x.SourceEntityId == seed.SourceId)).ShouldBe(0);
		(await check.Set<NotificationIntent>().CountAsync(x => x.SourceId == seed.SourceId)).ShouldBe(0);
	}
	[NotificationPostgresFact]
	public async Task Concurrent_capture_waits_for_the_source_transaction_and_returns_one_persisted_operation()
	{
		var seed = await SeedAsync("published"); await using var first = fixture.CreateContext(); await using var second = fixture.CreateContext();
		await using var firstTransaction = await first.Database.BeginTransactionAsync();
		await using var secondTransaction = await second.Database.BeginTransactionAsync();
		var owner = CreateWriter(first, seed); var contender = CreateWriter(second, seed);
		var operation = await owner.Writer.WriteAsync(Input(seed), default); operation.ShouldNotBeNull();
		var duplicate = contender.Writer.WriteAsync(Input(seed) with { OccurredAt = Now.AddSeconds(1) }, default);
		await contender.LockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
		(await Task.WhenAny(duplicate, Task.Delay(150))).ShouldNotBe(duplicate);
		await firstTransaction.CommitAsync();
		(await duplicate.WaitAsync(TimeSpan.FromSeconds(10))).ShouldBe(operation);
		await secondTransaction.CommitAsync();
		await using var check = fixture.CreateContext();
		(await check.Set<CommunicationCampaign>().CountAsync(x => x.SourceEntityId == seed.SourceId)).ShouldBe(1);
		(await check.Set<NotificationIntent>().CountAsync(x => x.SourceId == seed.SourceId)).ShouldBe(1);
		(await check.Set<NotificationIntent>().SingleAsync(x => x.SourceId == seed.SourceId)).CampaignId.ShouldBe(operation);
	}
	[NotificationPostgresFact]
	public async Task Competing_due_scans_commit_one_cursor_and_only_remind_the_currently_eligible_source()
	{
		var pending = await SeedAsync("published", DueEvent);
		var completed = await SeedAsync("submitted", DueEvent, pending.Tenant, seedRule: false);
		var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
		var year = new AcademicYear(Guid.NewGuid(), pending.Tenant, "Academic year", start, start.AddDays(29));
		var scope = new NotificationSourceScopeDto(year.Id, Guid.NewGuid(), Guid.NewGuid());
		var calendar = new AcademicCalendar(Guid.NewGuid(), pending.Tenant, scope.MediumId!.Value, year.Id, "Published calendar", start, start.AddDays(29));
		calendar.Publish(Guid.NewGuid());
		await using (var db = fixture.CreateContext())
		{
			db.AddRange(year, calendar, new AcademicCalendarDay(Guid.NewGuid(), pending.Tenant, calendar.Id, Now.UtcDateTime.Date,
				DayType.EmergencyClosure, "School closure"));
			await db.SaveChangesAsync();
		}

		var deadline = Now.AddDays(1);
		var candidates = new[] { pending, completed }.Select(x => new NotificationDueSourceCandidate(DueEvent, x.SourceId,
			"version-1", scope, deadline, deadline, NotificationDueTimeMode.PrecedingSchoolDays)).ToArray();
		var sourceEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondLockAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		await using var first = fixture.CreateContext();
		await using var second = fixture.CreateContext();
		await using var firstTx = await first.Database.BeginTransactionAsync();
		await using var secondTx = await second.Database.BeginTransactionAsync();
		var firstScheduler = CreateScheduler(first, firstTx, pending, scope, async cancellationToken => {
			sourceEntered.SetResult();
			await release.Task.WaitAsync(cancellationToken);
			return new(candidates, null);
		});
		var secondScheduler = CreateScheduler(second, secondTx, pending, scope, _ => Task.FromResult(new NotificationDueSourcePage(candidates, null)),
			secondLockAttempted);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
		var owner = firstScheduler.ScanAsync(Now, 100, timeout.Token);
		await sourceEntered.Task.WaitAsync(timeout.Token);
		var contender = secondScheduler.ScanAsync(Now, 100, timeout.Token);
		await secondLockAttempted.Task.WaitAsync(timeout.Token);
		(await Task.WhenAny(contender, Task.Delay(150))).ShouldNotBe(contender);
		release.SetResult();

		(await owner).ShouldBe(new NotificationScanResult(2, 1, 1, false));
		(await contender).ShouldBe(new NotificationScanResult(0, 0, 0, false));
		await using var check = fixture.CreateContext();
		(await check.Set<NotificationScheduleCursor>().CountAsync(x => x.TenantId == pending.Tenant)).ShouldBe(1);
		var intent = await check.Set<NotificationIntent>().SingleAsync(x => x.TenantId == pending.Tenant);
		intent.SourceId.ShouldBe(pending.SourceId);
		intent.DueAt.ShouldBe(new DateTimeOffset(2026, 11, 17, 11, 30, 0, TimeSpan.Zero));
	}

	private static NotificationDueScheduler CreateScheduler(NotificationPersistenceTestDbContext db, IDbContextTransaction transaction,
		(Guid Tenant, Guid SourceId, Guid StudentId) seed, NotificationSourceScopeDto scope,
		Func<CancellationToken, Task<NotificationDueSourcePage>> page, TaskCompletionSource? attempted = null)
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(seed.Tenant);
		var unit = Substitute.For<IUnitOfWork>();
		unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(async call => {
			var cancellationToken = call.Arg<CancellationToken>();
			await db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
		});
		var units = Substitute.For<IUnitOfWorkManager>(); units.Current.Returns((IUnitOfWork?)null);
		units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(unit);
		var locks = Substitute.For<INotificationPersonalWriteLock>();
		locks.AcquireAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(call => {
			attempted?.TrySetResult();
			return NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), transaction.GetDbTransaction(),
				seed.Tenant, call.Arg<IReadOnlyList<string>>(), call.Arg<CancellationToken>());
		});
		var dueSource = Substitute.For<INotificationDueSource>(); dueSource.SourceType.Returns("Academic.Homework");
		dueSource.ReadPageAsync(Arg.Any<NotificationDueScanRequest>(), Arg.Any<CancellationToken>())
			.Returns(call => page(call.Arg<CancellationToken>()));
		var source = new Source(db, seed.StudentId, DueEvent, scope);
		var catalogue = new AcademicNotificationCatalogue([source]);
		var executer = new AsyncQueryableExecuter([]); var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", seed.Tenant.ToString(), false).Returns((string?)null);
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		var calendar = new AcademicNotificationWorkingDays(tenant, executer, Repository<AcademicYear>(db), Repository<AcademicCalendar>(db),
			Repository<AcademicCalendarDay>(db), Substitute.For<ILectureSlotConfigRepository>(), Substitute.For<ITimetableRepository>());
		return new(tenant, units, locks, Repository<NotificationScheduleCursor>(db), executer, guids, [dueSource], [catalogue],
			new(tenant, Repository<NotificationRule>(db), executer), new(), new(tenant, settings), calendar, new(),
			CreateWriter(db, seed, sourceAdapter: source).Writer, NullLogger<NotificationDueScheduler>.Instance);
	}

	private async Task<(Guid Tenant, Guid SourceId, Guid StudentId)> SeedAsync(string sourceState, string eventKey = "Academic.Homework.Published",
		Guid? tenantId = null, bool seedRule = true)
	{
		var tenant = tenantId ?? Guid.NewGuid(); var sourceId = Guid.NewGuid(); var student = Guid.NewGuid();
		await using var db = fixture.CreateContext(); var templateId = Guid.NewGuid();
		db.AddRange(new CommunicationTemplate(sourceId, tenant) { TemplateName = "Synthetic business source", Body = sourceState },
			new CommunicationTemplate(templateId, tenant) { TemplateName = "Synthetic notification template", Body = "Safe template" });
		if (!seedRule) { await db.SaveChangesAsync(); return (tenant, sourceId, student); }
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, templateId, 1, eventKey, "Guardian",
			CommunicationChannelType.InApp, "en", "Safe title", "Safe body", 1); revision.Publish(Guid.NewGuid(), Now); db.Add(revision);
		var rule = new NotificationRule(Guid.NewGuid(), tenant, eventKey, NotificationRuleScope.Create());
		var definition = new AcademicNotificationCatalogue([]).Get(rule.EventKey);
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp], [templateId], NotificationRuleTimingConfiguration.Default, "en");
		rule.Configure(true, new NotificationRuleConfigurationCodec().Serialize(config, definition), rule.ConcurrencyStamp); db.Add(rule);
		await db.SaveChangesAsync(); return (tenant, sourceId, student);
	}
	private static NotificationEventInput Input((Guid Tenant, Guid SourceId, Guid StudentId) seed) => new("Academic.Homework.Published",
		"Academic.Homework", seed.SourceId, "version-1", Now, []);
	private static (INotificationIntentWriter Writer, TaskCompletionSource LockAttempted) CreateWriter(NotificationPersistenceTestDbContext db,
		(Guid Tenant, Guid SourceId, Guid StudentId) seed, bool breakIntentTenant = false, INotificationSourceAdapter? sourceAdapter = null)
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(seed.Tenant);
		var unit = Substitute.For<IUnitOfWork>(); unit.Options.Returns(new AbpUnitOfWorkOptions { IsTransactional = true });
		unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => db.SaveChangesAsync(call.Arg<CancellationToken>()));
		var units = Substitute.For<IUnitOfWorkManager>(); units.Current.Returns(unit);
		var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var transactions = Substitute.For<INotificationPersonalWriteLock>();
		transactions.AcquireAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(call => {
			acquired.TrySetResult();
			return NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction(),
				seed.Tenant, call.Arg<IReadOnlyList<string>>(), call.Arg<CancellationToken>());
		});
		var source = sourceAdapter ?? new Source(db, seed.StudentId); var catalogue = new AcademicNotificationCatalogue([source]);
		var executer = new AsyncQueryableExecuter([]); var config = new NotificationRuleConfigurationCodec();
		var settings = Substitute.For<ISettingManager>(); settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", seed.Tenant.ToString(), false).Returns((string?)null);
		var planner = new NotificationIntentCapturePlanner(tenant, [catalogue], [source], new(),
			new(tenant, Repository<NotificationRule>(db), executer), config, new(config),
			new(tenant, Repository<CommunicationTemplateRevision>(db), executer, config), new(tenant, settings), new());
		var intents = Repository<NotificationIntent>(db);
		if (breakIntentTenant) intents.InsertAsync(Arg.Any<NotificationIntent>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var intent = call.Arg<NotificationIntent>(); db.Add(intent); db.Entry(intent).Property(x => x.TenantId).CurrentValue = Guid.NewGuid(); return intent;
		});
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		var campaigns = Repository<CommunicationCampaign>(db);
		var dispatches = Repository<NotificationDispatch>(db);
		var outcomeLock = new NotificationOutcomeLock(transactions);
		var outcomes = new NotificationOutcomeProjector(tenant, units, outcomeLock, intents, dispatches, campaigns, executer);
		return (new NotificationIntentWriter(tenant, units, new NotificationEventCaptureLock(transactions), planner,
			campaigns, intents, dispatches, outcomeLock, outcomes, executer, guids, Substitute.For<IDataFilter>()), acquired);
	}
	private static IRepository<T, Guid> Repository<T>(DbContext db) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>(); repository.GetQueryableAsync().Returns(db.Set<T>());
		repository.InsertAsync(Arg.Any<T>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<T>(); db.Add(row); return row;
		}); return repository;
	}
	private sealed class Source(NotificationPersistenceTestDbContext db, Guid studentId, string eventKey = "Academic.Homework.Published",
		NotificationSourceScopeDto? scope = null) : INotificationSourceAdapter
	{
		public IReadOnlyCollection<string> EventKeys => [eventKey];
		public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
		{
			var state = await db.Set<CommunicationTemplate>().Where(x => x.Id == context.SourceId && x.TenantId == context.TenantId).Select(x => x.Body).SingleAsync(cancellationToken);
			if (scope != null)
			{
				context.SourceScope.ShouldBe(scope);
				context.DueOccurrenceAt.ShouldBe(Now.AddDays(1));
			}
			var recipient = new NotificationRecipientRefDto("Student", StudentId: studentId);
			return new(state == "published", null, "version-1", "/academic/tasks", new Dictionary<string, NotificationVariableValueDto>(), [recipient],
				Now.AddDays(1), [new(scope ?? new(), [recipient])]);
		}
	}
}
