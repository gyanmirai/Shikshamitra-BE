using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationApplicationModel_Tests
{
	[Fact]
	public void Full_application_model_preserves_required_tenant_keys_regardless_of_configuration_order()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Database=notification_tests_model;Username=notification_tests").Options);
		var entities = context.Model.GetEntityTypes()
			.Where(x => x.ClrType.Namespace == typeof(NotificationIntent).Namespace).ToList();
		entities.Count.ShouldBe(11);
		entities.ShouldAllBe(x => !x.FindProperty("TenantId")!.IsNullable);
		foreach (var entity in entities)
		{
			entity.GetQueryFilter().ShouldNotBeNull();
			entity.GetQueryFilter()!.ToString().ShouldContain("TenantId");
		}
		var intent = context.Model.FindEntityType(typeof(NotificationIntent))!;
		intent.GetForeignKeys().ShouldContain(x => x.PrincipalEntityType.ClrType == typeof(NotificationRule)
			&& x.Properties.Any(property => property.Name == "TenantId"));
		context.Database.GetDbConnection().State.ShouldBe(System.Data.ConnectionState.Closed);
	}
}
