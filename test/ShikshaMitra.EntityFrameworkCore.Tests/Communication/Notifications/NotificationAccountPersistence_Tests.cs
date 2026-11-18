using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationAccountPersistence_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Inbox_deduplicates_one_childs_event_without_merging_a_siblings_message()
	{
		var tenant = Guid.NewGuid();
		var user = Guid.NewGuid();
		await using var context = fixture.CreateContext();
		var dispatch = await SeedDispatchAsync(context, tenant);
		context.Add(Inbox(tenant, user, dispatch, "child-one"));
		context.Add(Inbox(tenant, user, dispatch, "child-two"));
		await context.SaveChangesAsync();
		context.Add(Inbox(tenant, user, dispatch, "child-one"));
		await ExpectUniqueAsync(() => context.SaveChangesAsync());
	}

	[NotificationPostgresFact]
	public async Task User_level_preferences_use_a_nonnullable_unique_context()
	{
		var tenant = Guid.NewGuid();
		var user = Guid.NewGuid();
		await using var context = fixture.CreateContext();
		context.Add(new NotificationPreference(Guid.NewGuid(), tenant, user, "Homework"));
		context.Add(new NotificationPreference(Guid.NewGuid(), tenant, user, "Homework", Guid.NewGuid()));
		await context.SaveChangesAsync();
		context.Add(new NotificationPreference(Guid.NewGuid(), tenant, user, "Homework"));
		await ExpectUniqueAsync(() => context.SaveChangesAsync());
	}

	[NotificationPostgresFact]
	public async Task Active_push_endpoint_cannot_be_registered_as_another_users_subscription()
	{
		var tenant = Guid.NewGuid();
		await using var context = fixture.CreateContext();
		context.Add(new WebPushSubscription(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(),
			"endpoint-hash", "protected-subscription", "v1", DateTimeOffset.UtcNow));
		await context.SaveChangesAsync();
		context.Add(new WebPushSubscription(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(),
			"endpoint-hash", "protected-subscription", "v1", DateTimeOffset.UtcNow));
		await ExpectUniqueAsync(() => context.SaveChangesAsync());
	}

	[NotificationPostgresFact]
	public async Task Callback_deduplication_uses_the_provider_account_not_only_the_message_key()
	{
		var tenant = Guid.NewGuid();
		await using var context = fixture.CreateContext();
		var first = new ChannelProviderSetting(Guid.NewGuid(), tenant) { ProviderName = "MetaOne" };
		var second = new ChannelProviderSetting(Guid.NewGuid(), tenant) { ProviderName = "MetaTwo" };
		context.AddRange(first, second);
		await context.SaveChangesAsync();
		context.Add(new NotificationProviderEvent(Guid.NewGuid(), tenant, first.Id, "event-key", "protected", DateTimeOffset.UtcNow));
		context.Add(new NotificationProviderEvent(Guid.NewGuid(), tenant, second.Id, "event-key", "protected", DateTimeOffset.UtcNow));
		await context.SaveChangesAsync();
		context.Add(new NotificationProviderEvent(Guid.NewGuid(), tenant, first.Id, "event-key", "protected", DateTimeOffset.UtcNow));
		await ExpectUniqueAsync(() => context.SaveChangesAsync());
	}

	internal static async Task<NotificationDispatch> SeedDispatchAsync(NotificationPersistenceTestDbContext context, Guid tenant,
		CommunicationChannelType channel = CommunicationChannelType.Email)
	{
		const string eventKey = "Academic.Homework.Published";
		var scope = NotificationRuleScope.Create();
		var rule = await context.Set<NotificationRule>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.EventKey == eventKey && x.ScopeKey == scope.Key);
		if (rule == null)
		{
			rule = new NotificationRule(Guid.NewGuid(), tenant, eventKey, scope);
			context.Add(rule);
		}
		var header = new CommunicationTemplate(Guid.NewGuid(), tenant) { TemplateName = "Homework", Subject = "Homework", Body = "Body" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, header.Id, 1,
			eventKey, "PrimaryGuardian", channel, "en", "Homework", "Body", 1);
		var intent = NotificationPersistence_Tests.Intent(tenant, rule.Id, Guid.NewGuid().ToString("N"));
		var dispatch = new NotificationDispatch(Guid.NewGuid(), tenant, intent.Id, revision.Id, "{}", "student-context",
			channel, "fingerprint", "protected", "hash", Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
		context.AddRange(header, revision, intent, dispatch);
		await context.SaveChangesAsync();
		return dispatch;
	}

	private static NotificationInboxItem Inbox(Guid tenant, Guid user, NotificationDispatch dispatch, string childContext) =>
		new(Guid.NewGuid(), tenant, user, dispatch.Id, "Academic.Homework", Guid.NewGuid(), childContext,
			"homework-published", "Homework available", "Open the portal", "/academic/homeworkCreation");

	private static async Task ExpectUniqueAsync(Func<Task> save)
	{
		var error = await Should.ThrowAsync<DbUpdateException>(save);
		error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
	}
}
