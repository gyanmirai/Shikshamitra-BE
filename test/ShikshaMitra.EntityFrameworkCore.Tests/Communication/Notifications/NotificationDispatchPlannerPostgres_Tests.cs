using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
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
		TaskCompletionSource? loadedSource = null)
	{
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns(work.TenantId);
		var unit = Substitute.For<IUnitOfWork>();
		unit.Options.Returns(new AbpUnitOfWorkOptions { IsTransactional = true });
		unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => db.SaveChangesAsync(call.Arg<CancellationToken>()));
		var units = Substitute.For<IUnitOfWorkManager>();
		units.Current.Returns(unit);
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
		return new(tenant, units, clock, new(tenant, [catalogue], [adapter], new(new()), new()), [resolver],
			new(tenant, new(tenant, Repository<NotificationRule>(db), queries), Repository<NotificationPreference>(db),
				Repository<ConsentRecord>(db), queries, new(tenant, settings), new()),
			new(tenant, Repository<ChannelProviderSetting>(db), queries),
			new(tenant, revisions, queries, renderer, validator, app, new(new EphemeralDataProtectionProvider(), tenant), ids),
			Repository<NotificationIntent>(db), Repository<CommunicationCampaign>(db), Repository<NotificationDispatch>(db),
			Repository<CommunicationRecipientSnapshot>(db), queries, ids);
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
