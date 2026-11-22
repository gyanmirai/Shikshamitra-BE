using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class ReviewedAcademicNotificationMigrationPostgres_Tests(NotificationPostgresFixture fixture)
	: IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Upgrade_keeps_saved_rule_intents_and_allows_only_reviewed_manual_to_omit_the_rule()
	{
		var migration = new AddReviewedAcademicNotificationSending();
		var tenant = Guid.NewGuid();
		var actor = Guid.NewGuid();
		var now = DateTimeOffset.UtcNow;
		var legacy = new NotificationSendReview(Guid.NewGuid(), tenant, actor, "previous-review", now.AddMinutes(10));
		Guid automatic;
		await using (var seed = fixture.CreateContext())
		{
			automatic = (await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant)).IntentId;
			seed.Add(legacy);
			await seed.SaveChangesAsync();
		}

		await ExecuteAsync(migration.DownOperations);
		await ExecuteAsync(migration.UpOperations);
		await using (var check = fixture.CreateContext())
		{
			var previous = await check.Set<NotificationSendReview>().SingleAsync(x => x.Id == legacy.Id);
			previous.Digest.ShouldBe(legacy.Digest);
			previous.ProtectedRequest.ShouldBeNull();
			var auto = await check.Set<NotificationIntent>().SingleAsync(x => x.Id == automatic);
			auto.RuleId.ShouldNotBeNull();
			auto.Origin.ShouldBe(NotificationIntentOrigin.Automatic);

			var manual = NotificationIntent.CreateReviewedManual(Guid.NewGuid(), tenant, null, 0,
				"Academic.Homework.Published", "Academic.Homework", Guid.NewGuid(), "reviewed-source", Guid.NewGuid().ToString("N"),
				now, "[]", "[]", now.AddHours(1), actor, Guid.NewGuid(), "{\"schemaVersion\":1}");
			check.Add(manual);
			await check.SaveChangesAsync();
			(await check.Set<NotificationIntent>().AsNoTracking().SingleAsync(x => x.Id == manual.Id)).RuleId.ShouldBeNull();
			check.Entry(auto).Property(x => x.RuleId).CurrentValue = null;
			check.Entry(auto).Property(x => x.RuleRevision).CurrentValue = 0;
			await Should.ThrowAsync<DbUpdateException>(() => check.SaveChangesAsync());
		}
	}

	private async Task ExecuteAsync(IReadOnlyList<MigrationOperation> operations)
	{
		await using var connection = fixture.CreateConnection();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options);
		var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations);
		await connection.OpenAsync();
		foreach (var item in commands)
		{
			await using var command = new NpgsqlCommand(item.CommandText, connection);
			await command.ExecuteNonQueryAsync();
		}
	}
}
