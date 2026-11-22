using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateHeaderMigration_Tests
{
	[Fact]
	public void Header_reference_upgrade_only_adds_its_column_keys_and_constraints()
	{
		var migration = CreateMigration();
		var type = migration.GetType();
		var up = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
		type!.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [up]);
		var column = up.Operations.OfType<AddColumnOperation>().Single();
		column.Table.ShouldBe("CommunicationTemplates"); column.Name.ShouldBe("CurrentNotificationRevisionId"); column.IsNullable.ShouldBeTrue();
		up.Operations.Any(x => x is CreateTableOperation or DropTableOperation or AlterColumnOperation or UpdateDataOperation or DeleteDataOperation or SqlOperation).ShouldBeFalse();
		var reference = up.Operations.OfType<AddForeignKeyOperation>().Single();
		reference.Columns.ShouldBe(new[] { "TenantId", "Id", "CurrentNotificationRevisionId" });
		reference.PrincipalColumns.ShouldBe(new[] { "TenantId", "TemplateId", "Id" });
		reference.OnDelete.ShouldBe(ReferentialAction.Restrict);
		up.Operations.OfType<AddCheckConstraintOperation>().Single().Name.ShouldBe("CK_CommTemplate_NotificationTenant");
		var down = new MigrationBuilder(up.ActiveProvider);
		type.GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [down]);
		down.Operations.OfType<DropColumnOperation>().Single().Name.ShouldBe("CurrentNotificationRevisionId");
		down.Operations.Any(x => x is DropTableOperation or DeleteDataOperation or SqlOperation).ShouldBeFalse();
	}

	internal static Migration CreateMigration()
	{
		var type = typeof(ShikshaMitraDbContext).Assembly.GetTypes().SingleOrDefault(x => x.Name == "AddNotificationTemplateHeaderRevision");
		type.ShouldNotBeNull();
		return (Migration)Activator.CreateInstance(type!)!;
	}
}
