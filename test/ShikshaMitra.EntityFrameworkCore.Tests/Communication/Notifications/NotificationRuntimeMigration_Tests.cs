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
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationRuntimeMigration_Tests
{
	[Fact]
	public void Runtime_upgrade_only_adds_owned_fields_checks_and_recovery_indexes()
	{
		var migration = CreateMigration(); var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
		columns.Length.ShouldBe(7);
		columns.Select(x => x.Table).Distinct().OrderBy(x => x).ShouldBe(new[] { "NotificationDispatches", "NotificationIntents" });
		columns.Single(x => x.Name == "RuleConfigurationJson").DefaultValue.ShouldBe("{}");
		columns.Single(x => x.Name == "Origin").DefaultValue.ShouldBe(0);
		columns.Where(x => x.Name is not ("RuleConfigurationJson" or "Origin")).ShouldAllBe(x => x.IsNullable);
		migration.UpOperations.OfType<AddCheckConstraintOperation>().Count().ShouldBe(2);
		migration.UpOperations.OfType<CreateIndexOperation>().Count().ShouldBe(2);
		migration.UpOperations.ShouldAllBe(x => x is AddColumnOperation || x is AddCheckConstraintOperation || x is CreateIndexOperation);
		migration.DownOperations.ShouldAllBe(x => x is DropColumnOperation || x is DropCheckConstraintOperation || x is DropIndexOperation);
	}
	internal static Migration CreateMigration()
	{
		var type = typeof(ShikshaMitraDbContext).Assembly.GetTypes()
			.SingleOrDefault(x => typeof(Migration).IsAssignableFrom(x) && x.Name == "AddNotificationRuntimeSafety");
		type.ShouldNotBeNull("the additive runtime safety migration must exist");
		return (Migration)Activator.CreateInstance(type)!;
	}
}

public class NotificationRuntimeMigrationPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Upgrade_preserves_old_work_without_making_legacy_intents_executable()
	{
		var migration = NotificationRuntimeMigration_Tests.CreateMigration();
		var tenant = Guid.NewGuid(); Guid intentId; Guid dispatchId; Guid revisionId; string originalHash;
		await using (var seed = fixture.CreateContext())
		{
			var dispatch = await NotificationAccountPersistence_Tests.SeedDispatchAsync(seed, tenant);
			dispatchId = dispatch.Id; intentId = dispatch.IntentId; revisionId = dispatch.TemplateRevisionId;
			var revision = await seed.Set<CommunicationTemplateRevision>().SingleAsync(x => x.Id == revisionId);
			originalHash = revision.ContentHash;
		}
		await ExecuteAsync(migration.DownOperations);
		await using (var connection = fixture.CreateConnection())
		{
			await connection.OpenAsync();
			await using var old = new NpgsqlCommand("SELECT \"AffectedRecipientsJson\" FROM \"NotificationIntents\" WHERE \"Id\" = @id", connection);
			old.Parameters.AddWithValue("id", intentId); (await old.ExecuteScalarAsync()).ShouldBe("[]");
		}
		await ExecuteAsync(migration.UpOperations);
		await using (var check = fixture.CreateContext())
		{
			var intent = await check.Set<NotificationIntent>().SingleAsync(x => x.Id == intentId);
			intent.RuleConfigurationJson.ShouldBe("{}"); intent.Origin.ShouldBe(NotificationIntentOrigin.Automatic);
			intent.PlanningCompletedAt.ShouldBeNull(); intent.Status.ShouldNotBe(NotificationIntentState.Completed);
			var dispatch = await check.Set<NotificationDispatch>().SingleAsync(x => x.Id == dispatchId);
			dispatch.IoStartedAt.ShouldBeNull(); dispatch.IoLeaseId.ShouldBeNull(); dispatch.AcceptedAt.ShouldBeNull();
			var revision = await check.Set<CommunicationTemplateRevision>().SingleAsync(x => x.Id == revisionId);
			revision.ContentHash.ShouldBe(originalHash);
			var rule = await check.Set<NotificationRule>().SingleAsync(x => x.Id == intent.RuleId);
			rule.IsEnabled.ShouldBeFalse(); rule.ConfigurationJson.ShouldBe("{}");
		}
		await ExecuteAsync(migration.DownOperations);
		await using var verify = fixture.CreateContext();
		(await verify.Set<CommunicationTemplateRevision>().SingleAsync(x => x.Id == revisionId)).ContentHash.ShouldBe(originalHash);
	}
	private async Task ExecuteAsync(IReadOnlyList<MigrationOperation> operations)
	{
		await using var connection = fixture.CreateConnection();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options);
		var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations); await connection.OpenAsync();
		foreach (var item in commands)
		{
			await using var command = new NpgsqlCommand(item.CommandText, connection); await command.ExecuteNonQueryAsync();
		}
	}
}
