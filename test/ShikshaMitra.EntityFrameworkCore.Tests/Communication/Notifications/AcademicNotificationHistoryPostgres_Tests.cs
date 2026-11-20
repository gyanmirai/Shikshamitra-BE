using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class AcademicNotificationHistoryPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task History_queries_page_attempts_and_exclusions_on_postgresql()
	{
		await using var db = fixture.CreateContext();
		var tenant = Guid.NewGuid();
		var dispatch = await NotificationAccountPersistence_Tests.SeedDispatchAsync(db, tenant);
		var intent = await db.Set<NotificationIntent>().SingleAsync(x => x.Id == dispatch.IntentId);
		var operation = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		intent.BindAutomaticOperation(operation);
		var recipient = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant) { CampaignId = operation, RecipientType = "Guardian" };
		var skipped = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant)
		{
			CampaignId = operation, RecipientType = "Guardian", IsEligible = false, BlockedReason = "EmailMissing",
			CreationTime = now.UtcDateTime.AddSeconds(-1)
		};
		dispatch.BindPlanningEvidence(operation, recipient.Id, null);
		var lease = Guid.NewGuid();
		var entry = db.Entry(dispatch);
		entry.Property(x => x.Status).CurrentValue = NotificationDispatchState.Processing;
		entry.Property(x => x.LeaseId).CurrentValue = lease;
		entry.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(5);
		entry.Property(x => x.AttemptNumber).CurrentValue = 1;
		var attempt = new DeliveryAttempt(Guid.NewGuid(), tenant)
		{
			CampaignId = operation, NotificationDispatchId = dispatch.Id, RecipientSnapshotId = recipient.Id,
			Channel = dispatch.Channel, Status = CommunicationDeliveryStatus.Sent, AttemptNumber = 1,
			SentAt = now, CreationTime = now.UtcDateTime
		};
		dispatch.BeginIo(lease, now, attempt.Id);
		dispatch.RecordOutcome(lease, now, NotificationDispatchState.ProviderAccepted);
		db.AddRange(recipient, skipped, attempt);
		await db.SaveChangesAsync();

		var sut = new AcademicNotificationHistoryRows(Repository<NotificationDispatch>(db), Repository<DeliveryAttempt>(db),
			Repository<CommunicationRecipientSnapshot>(db), Repository<NotificationIntent>(db),
			Repository<CommunicationCampaign>(db), new AsyncQueryableExecuter([]));
		var definition = new AcademicNotificationCatalogue([]).Get(AcademicNotificationEventKeys.HomeworkPublished);
		var counts = await sut.GetCountsAsync(tenant, [intent.Id], default);
		counts.Single().ShouldBe(new AcademicNotificationDeliveryCountDto(dispatch.Channel, NotificationDispatchState.ProviderAccepted, 1));
		var first = await sut.GetPageAsync(tenant, [intent.Id], [operation], definition, new() { MaxResultCount = 1 }, default);
		first.TotalCount.ShouldBe(2);
		first.Items.Single().Status.ShouldBe(NotificationDispatchState.ProviderAccepted);
		var last = await sut.GetPageAsync(tenant, [intent.Id], [operation], definition, new() { SkipCount = 1, MaxResultCount = 1 }, default);
		last.Items.Single().ReasonCode.ShouldBe("EmailMissing");
		var none = await sut.GetPageAsync(tenant, [], [], definition, new() { MaxResultCount = 1 }, default);
		none.TotalCount.ShouldBe(0);
	}

	[NotificationPostgresFact]
	public async Task Pending_approvals_page_only_assigned_operations_with_current_source_access()
	{
		await using var db = fixture.CreateContext();
		var tenant = Guid.NewGuid();
		var approver = Guid.NewGuid();
		var actor = Guid.NewGuid();
		var source = Guid.NewGuid();
		const string sourceType = "Academic.Homework";
		const string eventKey = AcademicNotificationEventKeys.HomeworkPublished;
		var protection = new EphemeralDataProtectionProvider();
		var now = DateTimeOffset.UtcNow;
		for (var index = 1; index <= 15; index++)
		{
			var campaign = new CommunicationCampaign(new Guid(index, 0, 0, new byte[8]),
				index == 14 ? Guid.NewGuid() : tenant)
			{
				CampaignType = NotificationCampaignTypes.Academic, SourceModule = CommunicationSourceModule.Academic,
				SourceEntityType = sourceType, SourceEntityId = index == 1 ? Guid.NewGuid() : source,
				ApprovalRequired = true, EligibleRecipientCount = 101, CreationTime = now.UtcDateTime,
				Status = index == 15 ? CommunicationCampaignStatus.Queued : CommunicationCampaignStatus.PendingApproval
			};
			var review = new NotificationSendReview(Guid.NewGuid(), campaign.TenantId!.Value, actor, "digest", now.AddMinutes(10));
			var request = new PreviewAcademicNotificationManualDto(eventKey, sourceType, campaign.SourceEntityId!.Value, [], []);
			var guard = protection.CreateProtector("Academic.Notification.Review.v1",
				review.TenantId!.Value.ToString("N"), actor.ToString("N"), review.Id.ToString("N"));
			review.BindRequest(guard.Protect(JsonSerializer.Serialize(new { request, scopes = Array.Empty<NotificationSourceScopeDto>() },
				new JsonSerializerOptions(JsonSerializerDefaults.Web))));
			review.AssignApprover(index == 13 ? Guid.NewGuid() : approver);
			review.Consume(campaign.Id, $"send-{index}", now);
			db.AddRange(campaign, review);
		}
		await db.SaveChangesAsync();

		var authorizer = Substitute.For<IAcademicNotificationSourceAuthorizer>();
		authorizer.EventKeys.Returns([eventKey]);
		authorizer.EnsureCanViewAsync(eventKey, Arg.Is<Guid>(id => id != source), default)
			.Returns(_ => Task.FromException(new AbpAuthorizationException()));
		var access = new AcademicNotificationAccessService(null!, null!, null!, null!, null!, [], [authorizer],
			new AcademicNotificationCatalogue([]));
		var queries = new AsyncQueryableExecuter([]);
		var receipts = Repository<NotificationSendReview>(db);
		var sut = new AcademicNotificationOperationAccess(access, null!, null!,
			new AcademicNotificationReviewService(receipts, queries, protection, null!, null!),
			Repository<CommunicationCampaign>(db), Repository<NotificationIntent>(db), receipts, queries, null!, null!);
		var scope = new UserScopeContext { TenantId = tenant, UserId = approver, IsAdmin = true };
		var first = await sut.ReadPendingApprovalsAsync(tenant, approver, scope, null, default);
		first.Items.Select(x => x.OperationId).ShouldBe(Enumerable.Range(2, 10).Select(i => new Guid(i, 0, 0, new byte[8])));
		first.NextAfterId.ShouldBe(first.Items[^1].OperationId);
		var second = await sut.ReadPendingApprovalsAsync(tenant, approver, scope, first.NextAfterId, default);
		second.Items.Single().OperationId.ShouldBe(new Guid(12, 0, 0, new byte[8]));
		second.NextAfterId.ShouldBeNull();
	}

	[NotificationPostgresFact]
	public async Task History_offers_retry_only_on_the_current_definitively_rejected_attempt()
	{
		await using var db = fixture.CreateContext();
		var tenant = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var dispatch = await NotificationAccountPersistence_Tests.SeedDispatchAsync(db, tenant);
		var intent = await db.Set<NotificationIntent>().SingleAsync(x => x.Id == dispatch.IntentId);
		var campaign = new CommunicationCampaign(Guid.NewGuid(), tenant)
		{
			CampaignType = NotificationCampaignTypes.Academic, SourceModule = CommunicationSourceModule.Academic,
			Status = CommunicationCampaignStatus.PartiallyFailed
		};
		intent.BindAutomaticOperation(campaign.Id);
		db.Entry(intent).Property(x => x.Status).CurrentValue = NotificationIntentState.PartiallyFailed;
		db.Entry(intent).Property(x => x.PlanningCompletedAt).CurrentValue = now;
		var recipient = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant) { CampaignId = campaign.Id, RecipientType = "Guardian" };
		dispatch.BindPlanningEvidence(campaign.Id, recipient.Id, null);
		var lease = Guid.NewGuid();
		var entry = db.Entry(dispatch);
		entry.Property(x => x.Status).CurrentValue = NotificationDispatchState.Processing;
		entry.Property(x => x.LeaseId).CurrentValue = lease;
		entry.Property(x => x.LeaseExpiresAt).CurrentValue = now.AddMinutes(5);
		entry.Property(x => x.AttemptNumber).CurrentValue = 5;
		var attempts = new[] { 4, 5 }.Select(number => new DeliveryAttempt(Guid.NewGuid(), tenant)
		{
			CampaignId = campaign.Id, NotificationDispatchId = dispatch.Id, RecipientSnapshotId = recipient.Id,
			Channel = dispatch.Channel, Status = CommunicationDeliveryStatus.Failed, AttemptNumber = number,
			ErrorCode = "SmtpTemporaryRejection", CreationTime = now.UtcDateTime.AddMinutes(number - 5)
		}).ToArray();
		dispatch.BeginIo(lease, now, attempts[1].Id);
		dispatch.RecordOutcome(lease, now, NotificationDispatchState.Failed, errorCode: "SmtpTemporaryRejection");
		db.AddRange(campaign, recipient);
		db.AddRange(attempts);
		await db.SaveChangesAsync();

		var sut = new AcademicNotificationHistoryRows(Repository<NotificationDispatch>(db), Repository<DeliveryAttempt>(db),
			Repository<CommunicationRecipientSnapshot>(db), Repository<NotificationIntent>(db),
			Repository<CommunicationCampaign>(db), new AsyncQueryableExecuter([]));
		var definition = new AcademicNotificationCatalogue([]).Get(AcademicNotificationEventKeys.HomeworkPublished);
		var page = await sut.GetPageAsync(tenant, [intent.Id], [campaign.Id], definition,
			new() { MaxResultCount = 2, State = NotificationDispatchState.Failed }, default, includeRetryActions: true);
		page.TotalCount.ShouldBe(2);
		page.Items.Where(x => x.CanRequestRetry).ShouldHaveSingleItem().AttemptId.ShouldBe(attempts[1].Id);
	}

	private static IRepository<TEntity, Guid> Repository<TEntity>(NotificationPersistenceTestDbContext db)
		where TEntity : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetQueryableAsync().Returns(db.Set<TEntity>().AsQueryable());
		return repository;
	}
}
