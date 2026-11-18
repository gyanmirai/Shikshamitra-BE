using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateHeaderMigrationPostgres_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Header_migration_round_trip_preserves_legacy_rows_and_published_revision_content()
	{
		var migration = NotificationTemplateHeaderMigration_Tests.CreateMigration();
		var header = new CommunicationTemplate(Guid.NewGuid(), Guid.NewGuid()) {
			TemplateName = "Preserved legacy header", Subject = "Original subject", Body = "Original body"
		};
		var host = new CommunicationTemplate(Guid.NewGuid(), null) { TemplateName = "Legacy host header", Body = "Host body" };
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), header.TenantId!.Value, header.Id, 1,
			"Academic.Homework.Published", "Guardian", CommunicationChannelType.Email, "en", "Version subject", "Version body", 1);
		revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		await using (var seed = fixture.CreateContext())
		{
			seed.AddRange(header, host); await seed.SaveChangesAsync();
			seed.Add(revision); await seed.SaveChangesAsync();
			header.SetNotificationRevision(revision.Id); await seed.SaveChangesAsync();
		}
		await ExecuteAsync(migration.DownOperations);
		await AssertContentsAsync();
		await ExecuteAsync(migration.UpOperations);
		await AssertContentsAsync();
		await using (var check = fixture.CreateContext())
		{
			(await check.Set<CommunicationTemplate>().SingleAsync(x => x.Id == header.Id)).CurrentNotificationRevisionId.ShouldBeNull();
			(await check.Set<CommunicationTemplate>().SingleAsync(x => x.Id == host.Id)).TenantId.ShouldBeNull();
		}
		await ExecuteAsync(migration.DownOperations);
		await AssertContentsAsync();

		async Task AssertContentsAsync()
		{
			await using var check = fixture.CreateContext();
			var preserved = await check.Set<CommunicationTemplate>().Where(x => x.Id == header.Id)
				.Select(x => new { x.Body, x.Subject, x.TenantId }).SingleAsync();
			preserved.Body.ShouldBe("Original body"); preserved.Subject.ShouldBe("Original subject"); preserved.TenantId.ShouldBe(header.TenantId);
			var version = await check.Set<CommunicationTemplateRevision>().SingleAsync(x => x.Id == revision.Id);
			version.Body.ShouldBe("Version body"); version.IsPublished.ShouldBeTrue(); version.ContentHash.ShouldBe(revision.ContentHash);
		}
	}

	private async Task ExecuteAsync(System.Collections.Generic.IReadOnlyList<Microsoft.EntityFrameworkCore.Migrations.Operations.MigrationOperation> operations)
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
