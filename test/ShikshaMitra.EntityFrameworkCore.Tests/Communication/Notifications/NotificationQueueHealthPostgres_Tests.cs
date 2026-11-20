using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public sealed class NotificationQueueHealthPostgres_Tests(NotificationPostgresFixture fixture)
	: IClassFixture<NotificationPostgresFixture>
{
	private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

	[NotificationPostgresFact]
	public async Task Snapshot_counts_due_work_and_channel_outcomes_only_for_the_current_school()
	{
		var school = Guid.NewGuid();
		await SeedAsync(school);
		await SeedAsync(Guid.NewGuid());
		await using var connection = fixture.CreateConnection();

		var health = await NotificationPostgresQueueHealth.ReadAsync(connection, school, Now, default);

		health.DueIntents.ShouldBe(1);
		health.OldestDueIntentAt.ShouldBe(Now.AddMinutes(-20));
		health.PendingWhatsAppReceipts.ShouldBe(1);
		health.OldestWhatsAppReceiptAt.ShouldBe(Now.AddMinutes(-9));
		var whatsapp = health.Channels.Single(x => x.Channel == CommunicationChannelType.WhatsApp);
		whatsapp.Pending.ShouldBe(2);
		whatsapp.Due.ShouldBe(1);
		whatsapp.OldestDueAt.ShouldBe(Now.AddMinutes(-5));
		whatsapp.Processing.ShouldBe(1);
		whatsapp.UnknownAcceptance.ShouldBe(1);
		whatsapp.RecentFailures.ShouldBe(1);
		whatsapp.PendingRetries.ShouldBe(1);
		whatsapp.PendingThrottled.ShouldBe(1);
		whatsapp.RecentConsentExclusions.ShouldBe(1);
		whatsapp.RecentSetupExclusions.ShouldBe(1);
		health.Channels.Single(x => x.Channel == CommunicationChannelType.WebPush).RecentExpiredSubscriptions.ShouldBe(1);

		var empty = await NotificationPostgresQueueHealth.ReadAsync(connection, Guid.NewGuid(), Now, default);
		empty.DueIntents.ShouldBe(0);
		empty.OldestDueIntentAt.ShouldBeNull();
		empty.Channels.ShouldBeEmpty();
		empty.PendingWhatsAppReceipts.ShouldBe(0);
		empty.OldestWhatsAppReceiptAt.ShouldBeNull();
	}

	private async Task SeedAsync(Guid school)
	{
		await using var db = fixture.CreateContext();
		const string eventKey = AcademicNotificationEventKeys.HomeworkPublished;
		var rule = new NotificationRule(Guid.NewGuid(), school, eventKey, NotificationRuleScope.Create());
		var template = new CommunicationTemplate(Guid.NewGuid(), school) { TemplateName = "Queue sample", Body = "Sample" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), school, template.Id, 1, eventKey, "Guardian",
			CommunicationChannelType.WhatsApp, "en", "Sample", "Sample", 1);
		var provider = new ChannelProviderSetting(Guid.NewGuid(), school) {
			Channel = CommunicationChannelType.WhatsApp, ProviderName = "Example"
		};
		var intent = Intent(Now.AddMinutes(-20));
		db.AddRange(rule, template, revision, provider, intent, Intent(Now.AddMinutes(5)), Intent(Now.AddHours(-1), Now));
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Pending, Now.AddMinutes(-5));
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Pending, Now.AddMinutes(5),
			attempt: 1, reason: "WhatsAppRateLimited");
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Pending, Now.AddHours(-1), expiresAt: Now);
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Pending, Now.AddHours(-1), deleted: true);
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Processing, Now);
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.UnknownAcceptance, Now);
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Failed, Now, modified: Now.AddMinutes(-10));
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Failed, Now, modified: Now.AddHours(-2));
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Skipped, Now,
			modified: Now.AddMinutes(-1), reason: "ConsentRequired");
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Skipped, Now,
			modified: Now.AddMinutes(-2), reason: "WhatsAppTemplateNotApproved");
		AddDispatch(CommunicationChannelType.WhatsApp, NotificationDispatchState.Skipped, Now,
			modified: Now.AddHours(-2), reason: "ConsentRequired");
		AddDispatch(CommunicationChannelType.WebPush, NotificationDispatchState.Failed, Now,
			modified: Now.AddMinutes(-2), reason: "WebPushSubscriptionExpired");
		var processed = Receipt("wa:applied", Now.AddMinutes(-20));
		processed.MarkProcessed(Now.AddMinutes(-10));
		db.AddRange(processed, Receipt("wa:pending", Now.AddMinutes(-9)), Receipt("unrelated", Now.AddHours(-1)));
		await db.SaveChangesAsync();

		NotificationIntent Intent(DateTimeOffset dueAt, DateTimeOffset? expiresAt = null) =>
			new(Guid.NewGuid(), school, rule.Id, 1, eventKey, "Academic.Homework", Guid.NewGuid(), "v1",
				Guid.NewGuid().ToString("N"), Now.AddHours(-2), dueAt, "[]", "[]", expiresAt);

		NotificationProviderEvent Receipt(string key, DateTimeOffset received) =>
			new(Guid.NewGuid(), school, provider.Id, key, "protected", received, "fixture-id");

		void AddDispatch(CommunicationChannelType channel, NotificationDispatchState status, DateTimeOffset dueAt,
			int attempt = 0, string? reason = null, DateTimeOffset? modified = null,
			DateTimeOffset? expiresAt = null, bool deleted = false)
		{
			var row = new NotificationDispatch(Guid.NewGuid(), school, intent.Id, revision.Id, "{}", "sample",
				channel, "fingerprint", "protected", "hash", Guid.NewGuid().ToString("N"), dueAt, expiresAt);
			var entry = db.Add(row);
			entry.Property(x => x.Status).CurrentValue = status;
			entry.Property(x => x.AttemptNumber).CurrentValue = attempt;
			entry.Property(x => x.ErrorCode).CurrentValue = reason;
			entry.Property(x => x.LastModificationTime).CurrentValue = modified?.UtcDateTime;
			entry.Property(x => x.IsDeleted).CurrentValue = deleted;
		}
	}
}
