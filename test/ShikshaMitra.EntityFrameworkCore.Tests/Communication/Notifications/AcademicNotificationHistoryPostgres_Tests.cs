using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
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
			Repository<CommunicationRecipientSnapshot>(db), Repository<NotificationIntent>(db), new AsyncQueryableExecuter([]));
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

	private static IRepository<TEntity, Guid> Repository<TEntity>(NotificationPersistenceTestDbContext db)
		where TEntity : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<TEntity, Guid>>();
		repository.GetQueryableAsync().Returns(db.Set<TEntity>().AsQueryable());
		return repository;
	}
}
