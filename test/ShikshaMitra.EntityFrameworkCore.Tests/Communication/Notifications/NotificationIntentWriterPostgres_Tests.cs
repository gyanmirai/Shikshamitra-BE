using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
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
	private async Task<(Guid Tenant, Guid SourceId, Guid StudentId)> SeedAsync(string sourceState)
	{
		var tenant = Guid.NewGuid(); var sourceId = Guid.NewGuid(); var student = Guid.NewGuid();
		await using var db = fixture.CreateContext(); var templateId = Guid.NewGuid();
		db.AddRange(new CommunicationTemplate(sourceId, tenant) { TemplateName = "Synthetic business source", Body = sourceState },
			new CommunicationTemplate(templateId, tenant) { TemplateName = "Synthetic notification template", Body = "Safe template" });
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, templateId, 1, "Academic.Homework.Published", "Guardian",
			CommunicationChannelType.InApp, "en", "Safe title", "Safe body", 1); revision.Publish(Guid.NewGuid(), Now); db.Add(revision);
		var rule = new NotificationRule(Guid.NewGuid(), tenant, "Academic.Homework.Published", NotificationRuleScope.Create());
		var definition = new AcademicNotificationCatalogue([]).Get(rule.EventKey);
		var config = new NotificationRuleRuntimeConfiguration(1, NotificationGuardianMode.PrimaryOnly, [], false, [],
			[CommunicationChannelType.InApp], [templateId], NotificationRuleTimingConfiguration.Default, "en");
		rule.Configure(true, new NotificationRuleConfigurationCodec().Serialize(config, definition), rule.ConcurrencyStamp); db.Add(rule);
		await db.SaveChangesAsync(); return (tenant, sourceId, student);
	}
	private static NotificationEventInput Input((Guid Tenant, Guid SourceId, Guid StudentId) seed) => new("Academic.Homework.Published",
		"Academic.Homework", seed.SourceId, "version-1", Now, []);
	private static (INotificationIntentWriter Writer, TaskCompletionSource LockAttempted) CreateWriter(NotificationPersistenceTestDbContext db,
		(Guid Tenant, Guid SourceId, Guid StudentId) seed, bool breakIntentTenant = false)
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
		var source = new Source(db, seed.StudentId); var catalogue = new AcademicNotificationCatalogue([source]);
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
		return (new NotificationIntentWriter(tenant, units, new NotificationEventCaptureLock(transactions), planner,
			Repository<CommunicationCampaign>(db), intents, executer, guids, Substitute.For<IDataFilter>()), acquired);
	}
	private static IRepository<T, Guid> Repository<T>(DbContext db) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>(); repository.GetQueryableAsync().Returns(db.Set<T>());
		repository.InsertAsync(Arg.Any<T>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<T>(); db.Add(row); return row;
		}); return repository;
	}
	private sealed class Source(NotificationPersistenceTestDbContext db, Guid studentId) : INotificationSourceAdapter
	{
		public IReadOnlyCollection<string> EventKeys => ["Academic.Homework.Published"];
		public async Task<NotificationSourceSnapshot> LoadAsync(NotificationIntentContext context, CancellationToken cancellationToken)
		{
			var state = await db.Set<CommunicationTemplate>().Where(x => x.Id == context.SourceId && x.TenantId == context.TenantId).Select(x => x.Body).SingleAsync(cancellationToken);
			var recipient = new NotificationRecipientRefDto("Student", StudentId: studentId);
			return new(state == "published", null, "version-1", "/academic/tasks", new Dictionary<string, NotificationVariableValueDto>(), [recipient],
				Now.AddDays(1), [new(new(), [recipient])]);
		}
	}
}
