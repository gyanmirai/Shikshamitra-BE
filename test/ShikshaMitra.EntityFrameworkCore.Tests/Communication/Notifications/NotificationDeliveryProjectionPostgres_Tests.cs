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
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDeliveryProjectionPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	private static readonly DateTimeOffset Now = new(2026, 11, 19, 19, 0, 0, TimeSpan.Zero);

	[NotificationPostgresFact]
	public async Task Two_transactions_cannot_reserve_the_same_remaining_provider_capacity()
	{
		var (tenantId, firstId, secondId) = await SeedAsync();
		await using var first = fixture.CreateContext();
		await using var second = fixture.CreateContext();
		await using var firstTransaction = await first.Database.BeginTransactionAsync();
		await using var secondTransaction = await second.Database.BeginTransactionAsync();
		var firstDispatch = await first.Set<NotificationDispatch>().SingleAsync(x => x.Id == firstId);
		var secondDispatch = await second.Set<NotificationDispatch>().SingleAsync(x => x.Id == secondId);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var acquired = await CreateProjection(first, tenantId).ReserveAsync(firstDispatch, timeout.Token);
		acquired.AttemptId.ShouldNotBeNull();
		firstDispatch.BeginIo(firstDispatch.LeaseId!.Value, Now, acquired.AttemptId);
		await first.SaveChangesAsync(timeout.Token);

		var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var contender = CreateProjection(second, tenantId, attempted).ReserveAsync(secondDispatch, timeout.Token);
		await attempted.Task.WaitAsync(timeout.Token);
		(await Task.WhenAny(contender, Task.Delay(150, timeout.Token))).ShouldNotBe(contender);
		await firstTransaction.CommitAsync(timeout.Token);
		var deferred = await contender.WaitAsync(timeout.Token);
		deferred.AttemptId.ShouldBeNull();
		deferred.ExclusionCode.ShouldBe(NotificationErrorCodes.ProviderQuotaExhausted);
		deferred.RetryAt.ShouldBe(new DateTimeOffset(2026, 11, 20, 18, 30, 0, TimeSpan.Zero));
		await secondTransaction.CommitAsync(timeout.Token);

		await using var check = fixture.CreateContext();
		var attempt = await check.Set<DeliveryAttempt>().SingleAsync(x => x.TenantId == tenantId);
		attempt.NotificationDispatchId.ShouldBe(firstId);
		attempt.Status.ShouldBe(CommunicationDeliveryStatus.Queued);
	}

	private async Task<(Guid Tenant, Guid First, Guid Second)> SeedAsync()
	{
		var tenant = Guid.NewGuid();
		var provider = new ChannelProviderSetting(Guid.NewGuid(), tenant) {
			Channel = CommunicationChannelType.Email, ProviderName = "SMTP", Status = CommunicationProviderStatus.Enabled,
			IsEnabled = true, DailyQuota = 1
		};
		var template = new CommunicationTemplate(Guid.NewGuid(), tenant) { TemplateName = "Academic email", Body = "Template" };
		const string eventKey = "Academic.Homework.Published";
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, template.Id, 1, eventKey, "Guardian",
			CommunicationChannelType.Email, "en", "Homework", "Template", 1);
		var rule = new NotificationRule(Guid.NewGuid(), tenant, eventKey, NotificationRuleScope.Create());
		var campaign = new CommunicationCampaign(Guid.NewGuid(), tenant) { CampaignType = NotificationCampaignTypes.Academic };
		var intent = new NotificationIntent(Guid.NewGuid(), tenant, rule.Id, rule.Revision, eventKey, "Academic.Homework", Guid.NewGuid(),
			"revision", "intent-key", Now, Now, "[]", "[]", Now.AddDays(3));
		var recipient = new CommunicationRecipientSnapshot(Guid.NewGuid(), tenant) {
			CampaignId = campaign.Id, RecipientType = "Guardian", DisplayName = "Test recipient"
		};
		var first = Dispatch();
		var second = Dispatch();
		await using var db = fixture.CreateContext();
		db.AddRange(provider, template, revision, rule, campaign, intent, recipient, first, second);
		await db.SaveChangesAsync();
		return (tenant, first.Id, second.Id);

		NotificationDispatch Dispatch()
		{
			var dispatch = new NotificationDispatch(Guid.NewGuid(), tenant, intent.Id, revision.Id, "{}", "context",
				CommunicationChannelType.Email, "fingerprint", "encrypted", "hash", Guid.NewGuid().ToString("N"), Now, Now.AddDays(3));
			dispatch.BindPlanningEvidence(campaign.Id, recipient.Id, provider.Id);
			Set(dispatch, nameof(NotificationDispatch.Status), NotificationDispatchState.Processing);
			Set(dispatch, nameof(NotificationDispatch.LeaseId), Guid.NewGuid());
			Set(dispatch, nameof(NotificationDispatch.LeaseExpiresAt), Now.AddMinutes(5));
			Set(dispatch, nameof(NotificationDispatch.AttemptNumber), 1);
			return dispatch;
		}
	}

	private static NotificationDeliveryProjection CreateProjection(NotificationPersistenceTestDbContext db, Guid tenantId,
		TaskCompletionSource? attempted = null)
	{
		var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(tenantId);
		var settings = Substitute.For<ISettingManager>();
		settings.GetOrNullAsync(NotificationSettingNames.SchoolTimeZone, "T", tenantId.ToString(), false).Returns("Asia/Kolkata");
		var clock = Substitute.For<IClock>(); clock.Now.Returns(Now.UtcDateTime);
		var guids = Substitute.For<IGuidGenerator>(); guids.Create().Returns(_ => Guid.NewGuid());
		var locks = Substitute.For<INotificationPersonalWriteLock>();
		locks.AcquireAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(call => {
			attempted?.TrySetResult();
			return NotificationPostgresPersonalLocks.AcquireAsync(db.Database.GetDbConnection(), db.Database.CurrentTransaction?.GetDbTransaction(),
				tenantId, call.Arg<IReadOnlyList<string>>(), call.Arg<CancellationToken>());
		});
		return new(tenant, locks, new(tenant, settings), Repository<ChannelProviderSetting>(db), Repository<DeliveryAttempt>(db),
			new AsyncQueryableExecuter([]), guids, clock);
	}

	private static IRepository<T, Guid> Repository<T>(DbContext db) where T : class, IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(db.Set<T>());
		repository.InsertAsync(Arg.Any<T>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call => {
			var row = call.Arg<T>(); db.Add(row); return row;
		});
		return repository;
	}

	private static void Set<T>(T row, string property, object value) => typeof(T).GetProperty(property)!.SetValue(row, value);
}
