using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Security;

public sealed class RoleDeletionPostgresFactAttribute : FactAttribute
{
	public RoleDeletionPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(RoleDeletionPostgresFactory.ConnectionVariable)))
			Skip = "Set ROLE_DELETION_TEST_POSTGRES to a loopback PostgreSQL administrator connection.";
	}
}

public class RoleDeletionPostgresFactory : RoleDeletionTestFactory
{
	public const string ConnectionVariable = "ROLE_DELETION_TEST_POSTGRES";
	private readonly string _database = "role_deletion_tests_" + Guid.NewGuid().ToString("N");
	private bool _created;
	public string ConnectionString { get; private set; } = "";
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(
			[new("ConnectionStrings:Default", Environment.GetEnvironmentVariable(ConnectionVariable)),
			 new("Logging:LogLevel:Default", "Warning")]));
	}
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
			throw new InvalidOperationException("Only loopback PostgreSQL is allowed.");
		using (var administrator = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres" }.ConnectionString))
		{
			administrator.Open();
			using var create = new NpgsqlCommand($"CREATE DATABASE \"{_database}\"", administrator);
			create.ExecuteNonQuery();
			_created = true;
		}
		connection.Database = _database;
		ConnectionString = connection.ConnectionString;
		services.Configure<AbpDbContextOptions>(options => options.Configure(context => context.DbContextOptions.UseNpgsql(ConnectionString)));
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(ConnectionString).Options);
		db.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
	public override async ValueTask DisposeAsync()
	{
		await base.DisposeAsync();
		if (!_created) return;
		NpgsqlConnection.ClearAllPools();
		var connection = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };
		await using var administrator = new NpgsqlConnection(connection.ConnectionString);
		await administrator.OpenAsync();
		await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_database}\" WITH (FORCE)", administrator);
		await drop.ExecuteNonQueryAsync();
	}
}

[Trait("Module", "Security"), Trait("Tier", "Critical")]
public class RoleDeletionPostgresApi_Tests
{
	[RoleDeletionPostgresFact]
	public async Task Real_postgres_should_reproduce_the_fk_and_pass_role_deletion_contracts()
	{
		await using var factory = new RoleDeletionPostgresFactory();
		var original = await factory.SeedAsync(users: 1, employee: true);
		await using var connection = new NpgsqlConnection(factory.ConnectionString);
		await connection.OpenAsync();
		await using var delete = new NpgsqlCommand("DELETE FROM \"AbpRoles\" WHERE \"Id\" = @role", connection);
		delete.Parameters.AddWithValue("role", original.RoleId);
		var failure = await Should.ThrowAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
		failure.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
		failure.ConstraintName.ShouldBe("FK_AppEmployees_AbpRoles_RoleId");

		var contracts = new RoleDeletionApi_Tests(factory);
		await contracts.Assigned_role_should_return_409_with_exact_user_count_and_preserve_memberships();
		await contracts.Employee_without_login_should_block_deletion_including_archived_records(false);
		await contracts.Employee_without_login_should_block_deletion_including_archived_records(true);
		await contracts.Unassigned_role_should_delete_successfully();
		await contracts.Delete_should_require_permission_before_disclosing_usage();
		await contracts.Other_tenant_should_not_delete_or_discover_assigned_role();
		await contracts.Direct_application_service_should_enforce_the_dependency_rule();

		await using var insert = new NpgsqlCommand("""
			INSERT INTO "AbpUserRoles" ("UserId", "RoleId", "TenantId")
			SELECT "UserId", @target, "TenantId" FROM "AbpUserRoles" WHERE "RoleId" = @source LIMIT 1
			""", connection);
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, original.TenantId.ToString());
		var created = await client.PostAsJsonAsync("/api/identity/roles", new { name = "Concurrent assignment", isDefault = false, isPublic = true,
			extraProperties = new { color = "#008967", roleAudience = "Employee", employeeGroupAudience = "Both", employeeOnboardingAssignable = false } });
		created.IsSuccessStatusCode.ShouldBeTrue(await created.Content.ReadAsStringAsync());
		var unassigned = (await created.Content.ReadFromJsonAsync<IdentityRoleDto>())!;
		insert.Parameters.AddWithValue("target", unassigned.Id);
		insert.Parameters.AddWithValue("source", original.RoleId);
		await using var transaction = await connection.BeginTransactionAsync();
		insert.Transaction = transaction;
		await insert.ExecuteNonQueryAsync();
		var pendingDelete = client.DeleteAsync("/api/identity/roles/" + unassigned.Id);
		await Task.Delay(300);
		pendingDelete.IsCompleted.ShouldBeFalse("Deletion must wait for the in-flight role assignment's FK lock.");
		await transaction.CommitAsync();
		(await pendingDelete).StatusCode.ShouldBe(HttpStatusCode.Conflict);
	}
}
