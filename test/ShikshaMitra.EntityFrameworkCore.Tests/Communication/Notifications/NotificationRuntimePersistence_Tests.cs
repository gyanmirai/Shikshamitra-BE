using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationRuntimePersistence_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	private const string Snapshot = "{\"schemaVersion\":1,\"channels\":[2],\"fallbackLanguage\":\"en\"}";

	[NotificationPostgresFact]
	public async Task A_persisted_manual_snapshot_retains_its_actor_operation_and_configuration()
	{
		var tenant = Guid.NewGuid(); var operation = Guid.NewGuid(); var actor = Guid.NewGuid(); Guid id;
		await using (var db = fixture.CreateContext())
		{
			var rule = new NotificationRule(Guid.NewGuid(), tenant, "Academic.Homework.Published", NotificationRuleScope.Create());
			var row = new NotificationIntent(Guid.NewGuid(), tenant, rule.Id, 1, rule.EventKey, "Academic.Homework", Guid.NewGuid(), "1",
				Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "[]", "[]", actorUserId: actor);
			row.BindRuntimeSnapshot(Snapshot, NotificationIntentOrigin.ReviewedManual, operation);
			db.AddRange(rule, row); await db.SaveChangesAsync(); id = row.Id;
		}
		await using var verify = fixture.CreateContext(); var stored = await verify.Set<NotificationIntent>().SingleAsync(x => x.Id == id);
		stored.Origin.ShouldBe(NotificationIntentOrigin.ReviewedManual); stored.ActorUserId.ShouldBe(actor);
		stored.CampaignId.ShouldBe(operation); stored.RuleConfigurationJson.ShouldBe(Snapshot);
	}

	[NotificationPostgresFact]
	public void Runtime_snapshot_defaults_keep_unclassified_existing_intents_nonexecutable()
	{
		using var db = fixture.CreateContext(); var entity = db.Model.FindEntityType(typeof(NotificationIntent))!;
		entity.FindProperty(nameof(NotificationIntent.RuleConfigurationJson))!.GetDefaultValue().ShouldBe("{}");
		entity.FindProperty(nameof(NotificationIntent.RuleConfigurationJson))!.GetMaxLength().ShouldBe(65536);
		entity.FindProperty(nameof(NotificationIntent.ErrorCode))!.GetMaxLength().ShouldBe(160);
	}

	[NotificationPostgresFact]
	public async Task Native_manual_origin_constraint_rejects_missing_actor_operation_and_unknown_origin()
	{
		for (var variant = 0; variant < 3; variant++)
		{
			await using var db = fixture.CreateContext(); var tenant = Guid.NewGuid();
			var rule = new NotificationRule(Guid.NewGuid(), tenant, "Academic.Homework.Published", NotificationRuleScope.Create());
			var row = NotificationPersistence_Tests.Intent(tenant, rule.Id, Guid.NewGuid().ToString("N"));
			db.AddRange(rule, row);
			db.Entry(row).Property(x => x.Origin).CurrentValue = variant == 2 ? (NotificationIntentOrigin)999 : NotificationIntentOrigin.ReviewedManual;
			if (variant == 0) db.Entry(row).Property(x => x.CampaignId).CurrentValue = Guid.NewGuid();
			if (variant == 1) db.Entry(row).Property(x => x.ActorUserId).CurrentValue = Guid.NewGuid();
			var error = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
			error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		}
	}

	[NotificationPostgresFact]
	public async Task Native_io_marker_constraint_rejects_half_written_markers()
	{
		await using var db = fixture.CreateContext();
		var row = await NotificationAccountPersistence_Tests.SeedDispatchAsync(db, Guid.NewGuid());
		db.Entry(row).Property(x => x.IoStartedAt).CurrentValue = DateTimeOffset.UtcNow;
		var error = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
		error.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
	}

	[NotificationPostgresFact]
	public async Task Persisted_io_fence_allows_only_one_worker_to_cross_the_send_boundary()
	{
		var tenant = Guid.NewGuid(); Guid id;
		await using (var seed = fixture.CreateContext()) id = (await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant)).Id;
		var now = DateTimeOffset.UtcNow.AddSeconds(1);
		await using var connection = fixture.CreateConnection();
		var claim = (await NotificationPostgresQueueClaims.ClaimAsync(connection, null, tenant, NotificationQueueKind.Dispatch,
			now, 1, TimeSpan.FromMinutes(5), CancellationToken.None)).Single();
		await using var first = fixture.CreateContext(); await using var second = fixture.CreateContext();
		var firstRow = await first.Set<NotificationDispatch>().SingleAsync(x => x.Id == id);
		var secondRow = await second.Set<NotificationDispatch>().SingleAsync(x => x.Id == id);
		firstRow.BeginIo(claim.LeaseId, now); secondRow.BeginIo(claim.LeaseId, now);
		await first.SaveChangesAsync();
		await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
		await using var recovery = fixture.CreateContext();
		var crashed = await recovery.Set<NotificationDispatch>().SingleAsync(x => x.Id == id);
		crashed.RecoverExpiredLease(now.AddMinutes(6)).ShouldBeTrue(); await recovery.SaveChangesAsync();
		await using var later = fixture.CreateContext();
		var uncertain = await later.Set<NotificationDispatch>().SingleAsync(x => x.Id == id);
		uncertain.Status.ShouldBe(NotificationDispatchState.UnknownAcceptance); uncertain.IoLeaseId.ShouldBe(claim.LeaseId);
		uncertain.RecordOutcome(claim.LeaseId, now.AddMinutes(6), NotificationDispatchState.ProviderAccepted, "late-provider-id");
		await later.SaveChangesAsync();
		await using var verify = fixture.CreateContext();
		(await verify.Set<NotificationDispatch>().SingleAsync(x => x.Id == id)).Status.ShouldBe(NotificationDispatchState.ProviderAccepted);
	}
}
