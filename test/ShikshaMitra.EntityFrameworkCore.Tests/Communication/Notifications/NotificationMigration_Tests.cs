using System;
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

public class NotificationMigrationStructure_Tests
{
	[Fact]
	public void Migration_only_creates_and_drops_the_owned_notification_tables()
	{
		var migration = CreateMigration();
		var created = migration.UpOperations.OfType<CreateTableOperation>().ToList();
		created.Count.ShouldBe(11);
		migration.UpOperations.ShouldAllBe(x => x is CreateTableOperation || x is CreateIndexOperation);
		var names = created.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
		migration.UpOperations.OfType<CreateIndexOperation>().ShouldAllBe(x => names.Contains(x.Table));
		migration.DownOperations.ShouldAllBe(x => x is DropTableOperation);
		migration.DownOperations.Cast<DropTableOperation>().Select(x => x.Name).OrderBy(x => x)
			.ShouldBe(names.OrderBy(x => x));
	}

	internal static Migration CreateMigration()
	{
		var type = typeof(ShikshaMitraDbContext).Assembly.GetTypes()
			.SingleOrDefault(x => typeof(Migration).IsAssignableFrom(x) && x.Name == "AddAcademicNotificationPipeline");
		type.ShouldNotBeNull("the notification schema migration must be generated and compiled");
		return (Migration)Activator.CreateInstance(type)!;
	}
}

public class NotificationMigrationPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Migration_round_trip_keeps_existing_communication_templates_unchanged()
	{
		var migration = NotificationMigrationStructure_Tests.CreateMigration();
		await ExecuteAsync(migration.DownOperations);
		var header = new CommunicationTemplate(Guid.NewGuid(), Guid.NewGuid())
			{ TemplateName = "Legacy template", Subject = "Original subject", Body = "Original body" };
		await using (var context = fixture.CreateContext())
		{
			context.Add(header);
			await context.SaveChangesAsync();
		}
		await ExecuteAsync(migration.UpOperations);
		await using (var context = fixture.CreateContext())
		{
			context.Add(new NotificationRule(Guid.NewGuid(), header.TenantId!.Value,
				"Academic.Homework.Published", NotificationRuleScope.Create()));
			await context.SaveChangesAsync();
			(await context.Set<NotificationRule>().CountAsync()).ShouldBe(1);
		}
		await ExecuteAsync(migration.DownOperations);
		await using var check = fixture.CreateContext();
		var preserved = await check.Set<CommunicationTemplate>().SingleAsync(x => x.Id == header.Id);
		preserved.Body.ShouldBe("Original body");
		preserved.Subject.ShouldBe("Original subject");
		await using var connection = fixture.CreateConnection();
		await connection.OpenAsync();
		await using var count = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'NotificationRules'", connection);
		Convert.ToInt32(await count.ExecuteScalarAsync()).ShouldBe(0);
	}

	private async Task ExecuteAsync(System.Collections.Generic.IReadOnlyList<MigrationOperation> operations)
	{
		await using var connection = fixture.CreateConnection();
		await using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection).Options);
		var commands = context.GetService<IMigrationsSqlGenerator>().Generate(operations);
		await connection.OpenAsync();
		foreach (var item in commands)
		{
			await using var command = new NpgsqlCommand(item.CommandText, connection);
			await command.ExecuteNonQueryAsync();
		}
	}
}
