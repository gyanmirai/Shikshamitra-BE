using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationDeliveryAttemptMigration_Tests
{
	[Fact]
	public void Migration_adds_only_nullable_attempt_links_and_their_constraints_and_indexes()
	{
		var migration = CreateMigration();
		var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
		columns.Select(x => x.Name).Order().ShouldBe(new[] { "NotificationDispatchId", "NotificationProviderSettingId" });
		columns.ShouldAllBe(x => x.Table == "CommunicationDeliveryAttempts" && x.IsNullable);
		var key = migration.UpOperations.OfType<AddForeignKeyOperation>().Single();
		key.Columns.ShouldBe(new[] { "TenantId", "NotificationDispatchId" });
		key.PrincipalColumns.ShouldBe(new[] { "TenantId", "Id" });
		migration.UpOperations.OfType<CreateIndexOperation>().Count().ShouldBe(2);
		migration.UpOperations.ShouldAllBe(x => x is AddColumnOperation || x is AddForeignKeyOperation || x is CreateIndexOperation);
		migration.DownOperations.ShouldAllBe(x => x is DropColumnOperation || x is DropForeignKeyOperation || x is DropIndexOperation);
	}

	internal static Migration CreateMigration()
	{
		var type = typeof(ShikshaMitraDbContext).Assembly.GetTypes()
			.Single(x => typeof(Migration).IsAssignableFrom(x) && x.Name == "AddNotificationDeliveryAttempts");
		return (Migration)Activator.CreateInstance(type)!;
	}
}
