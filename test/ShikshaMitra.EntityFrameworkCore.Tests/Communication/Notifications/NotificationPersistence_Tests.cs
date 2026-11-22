using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationPersistence_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task School_scope_is_unique_inside_a_tenant_but_independent_across_tenants()
	{
		var tenant = Guid.NewGuid();
		await using var context = fixture.CreateContext();
		context.Add(Rule(tenant));
		context.Add(Rule(Guid.NewGuid()));
		await context.SaveChangesAsync();
		context.Add(Rule(tenant));
		await ExpectConstraintAsync(() => context.SaveChangesAsync(), PostgresErrorCodes.UniqueViolation);
	}

	[NotificationPostgresFact]
	public async Task Stale_rule_update_fails_instead_of_overwriting_a_newer_configuration()
	{
		var rule = Rule(Guid.NewGuid());
		await using (var seed = fixture.CreateContext())
		{
			seed.Add(rule);
			await seed.SaveChangesAsync();
		}
		await using var first = fixture.CreateContext();
		await using var second = fixture.CreateContext();
		var firstRule = await first.Set<NotificationRule>().SingleAsync(x => x.Id == rule.Id);
		var secondRule = await second.Set<NotificationRule>().SingleAsync(x => x.Id == rule.Id);
		firstRule.ConcurrencyStamp = Guid.NewGuid().ToString("N");
		await first.SaveChangesAsync();
		secondRule.ConcurrencyStamp = Guid.NewGuid().ToString("N");
		await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
	}

	[NotificationPostgresFact]
	public async Task Intent_deduplication_is_per_tenant_and_parent_tenant_cannot_be_forged()
	{
		var tenant = Guid.NewGuid();
		var rule = Rule(tenant);
		await using var context = fixture.CreateContext();
		context.Add(rule);
		await context.SaveChangesAsync();
		context.Add(Intent(tenant, rule.Id, "same-source-revision"));
		await context.SaveChangesAsync();
		context.Add(Intent(tenant, rule.Id, "same-source-revision"));
		await ExpectConstraintAsync(() => context.SaveChangesAsync(), PostgresErrorCodes.UniqueViolation);
		context.ChangeTracker.Clear();
		context.Add(Intent(Guid.NewGuid(), rule.Id, "another-source"));
		await ExpectConstraintAsync(() => context.SaveChangesAsync(), PostgresErrorCodes.ForeignKeyViolation);
	}

	[NotificationPostgresFact]
	public async Task A_referenced_template_revision_cannot_be_deleted_and_dispatch_cannot_duplicate()
	{
		var tenant = Guid.NewGuid();
		var rule = Rule(tenant);
		var header = new CommunicationTemplate(Guid.NewGuid(), tenant) { TemplateName = "Homework", Subject = "Homework", Body = "Body" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), tenant, header.Id, 1,
			"Academic.Homework.Published", "PrimaryGuardian", CommunicationChannelType.Email, "en", "Homework", "Body", 1);
		var intent = Intent(tenant, rule.Id, "revision-use");
		await using var context = fixture.CreateContext();
		context.AddRange(rule, header, revision, intent);
		await context.SaveChangesAsync();
		context.Add(Dispatch(tenant, intent.Id, revision.Id, "unique-delivery"));
		await context.SaveChangesAsync();
		context.Add(Dispatch(tenant, intent.Id, revision.Id, "unique-delivery"));
		await ExpectConstraintAsync(() => context.SaveChangesAsync(), PostgresErrorCodes.UniqueViolation);
		context.ChangeTracker.Clear();
		var deletion = await Should.ThrowAsync<PostgresException>(() => context.Set<CommunicationTemplateRevision>()
			.Where(x => x.Id == revision.Id).ExecuteDeleteAsync());
		deletion.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
	}

	private static async Task ExpectConstraintAsync(Func<Task> save, string sqlState)
	{
		var error = await Should.ThrowAsync<DbUpdateException>(save);
		error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(sqlState);
	}

	private static NotificationRule Rule(Guid tenant) => new(Guid.NewGuid(), tenant,
		"Academic.Homework.Published", NotificationRuleScope.Create());

	internal static NotificationIntent Intent(Guid tenant, Guid ruleId, string key) => new(Guid.NewGuid(), tenant,
		ruleId, 1, "Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(), "v1", key,
		DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "[]", "[]");

	private static NotificationDispatch Dispatch(Guid tenant, Guid intentId, Guid revisionId, string key) =>
		new(Guid.NewGuid(), tenant, intentId, revisionId, "{}", "student-context", CommunicationChannelType.Email,
			"destination-fingerprint", "protected-message", "content-hash", key, DateTimeOffset.UtcNow);
}
