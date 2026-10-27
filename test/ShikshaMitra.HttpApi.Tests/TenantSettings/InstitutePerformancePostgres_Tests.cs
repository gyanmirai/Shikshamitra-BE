using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Shouldly;
using Xunit;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings;

public class InstitutePerformancePostgresFactAttribute : FactAttribute
{
	public InstitutePerformancePostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INSTITUTE_PERFORMANCE_TEST_POSTGRES")))
			Skip = "Set INSTITUTE_PERFORMANCE_TEST_POSTGRES to a loopback PostgreSQL connection; the test creates and removes its own database.";
	}
}

[Trait("Module", "TenantSettings"), Trait("Tier", "Extended")]
public class InstitutePerformancePostgres_Tests
{
	[InstitutePerformancePostgresFact]
	public async Task Additive_migration_backfills_only_owned_unambiguous_rooms_and_rolls_back_without_losing_records()
	{
		var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("INSTITUTE_PERFORMANCE_TEST_POSTGRES"));
		if (settings.Host is not ("127.0.0.1" or "localhost" or "::1"))
			throw new InvalidOperationException("The migration fixture only accepts loopback PostgreSQL.");
		settings.Database = "postgres"; settings.Pooling = false; settings.Timeout = 10;
		await using var admin = new NpgsqlConnection(settings.ConnectionString);
		await admin.OpenAsync();
		var name = "institute_perf_tests_" + Guid.NewGuid().ToString("N");
		await ExecuteAsync(admin, $"CREATE DATABASE \"{name}\"");
		try
		{
			settings.Database = name;
			await using var connection = new NpgsqlConnection(settings.ConnectionString);
			await connection.OpenAsync();
			await ExecuteAsync(connection, """
				CREATE TABLE "UnrelatedSentinel" ("Value" text NOT NULL);
				INSERT INTO "UnrelatedSentinel" VALUES ('preserved');
				CREATE TABLE "academic_Rooms" ("Id" uuid PRIMARY KEY, "TenantId" uuid, "RoomName" text NOT NULL, "Features" jsonb);
				CREATE TABLE "academic_Divisions" ("Id" uuid PRIMARY KEY, "TenantId" uuid, "RoomNumber" text NOT NULL);
				CREATE TABLE "AppInstituteReportJobs" ("Id" uuid PRIMARY KEY, "TenantId" uuid, "Status" varchar(24) NOT NULL DEFAULT 'Queued', "RequestJson" text NOT NULL);
				INSERT INTO "academic_Rooms" ("Id", "TenantId", "RoomName") VALUES
				('10000000-0000-0000-0000-000000000001','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Unique'),
				('10000000-0000-0000-0000-000000000002','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Shared'),
				('10000000-0000-0000-0000-000000000003','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Shared'),
				('10000000-0000-0000-0000-000000000004','bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb','Foreign');
				INSERT INTO "academic_Divisions" VALUES
				('20000000-0000-0000-0000-000000000001','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','10000000-0000-0000-0000-000000000001'),
				('20000000-0000-0000-0000-000000000002','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',' unique '),
				('20000000-0000-0000-0000-000000000003','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Shared'),
				('20000000-0000-0000-0000-000000000004','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','10000000-0000-0000-0000-000000000004');
				INSERT INTO "AppInstituteReportJobs" VALUES
				('30000000-0000-0000-0000-000000000001','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Queued','{"ReportType":"shift-register"}'),
				('30000000-0000-0000-0000-000000000002','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Queued','{"reportType":"rooms-inventory"}');
				""");
			await using var context = new MigrationQueryContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(settings.ConnectionString).Options);
			var generator = context.GetService<IMigrationsSqlGenerator>();
			var migration = new ImproveInstituteSetupPerformance();
			foreach (var sql in generator.Generate(migration.UpOperations, migration.TargetModel)) await ExecuteAsync(connection, sql.CommandText);
			(await ScalarAsync<long>(connection, "SELECT count(*) FROM \"academic_Divisions\" WHERE \"RoomId\" = '10000000-0000-0000-0000-000000000001'")).ShouldBe(2);
			(await ScalarAsync<long>(connection, "SELECT count(*) FROM \"academic_Divisions\" WHERE \"RoomId\" IS NULL")).ShouldBe(2);
			(await ScalarAsync<string>(connection, "SELECT \"ReportTypeId\" FROM \"AppInstituteReportJobs\" WHERE \"Id\" = '30000000-0000-0000-0000-000000000002'")).ShouldBe("rooms-inventory");
			var deletion = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection, "DELETE FROM \"academic_Rooms\" WHERE \"Id\" = '10000000-0000-0000-0000-000000000001'"));
			deletion.SqlState.ShouldBeOneOf(PostgresErrorCodes.ForeignKeyViolation, PostgresErrorCodes.RestrictViolation);
			deletion.ConstraintName.ShouldBe("FK_academic_Divisions_academic_Rooms_RoomId");
			await ExecuteAsync(connection, """
				UPDATE "academic_Rooms" SET "Features" = '["Projector","Wi-Fi"]'::jsonb
				WHERE "Id" = '10000000-0000-0000-0000-000000000001';
				""");
			var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
			provider.GetDbContextAsync().Returns(Task.FromResult<ShikshaMitraDbContext>(context));
			var features = new RoomFeatureQuery(provider);
			foreach (var (operation, value) in new[] {
				(RoomFeatureTextOperator.EndsWith, "WI-FI"), (RoomFeatureTextOperator.StartsWith, "projector"),
				(RoomFeatureTextOperator.Equals, "Projector, Wi-Fi"), (RoomFeatureTextOperator.Contains, "tor, wi") })
			{
				var ids = await features.GetMatchingRoomIdsAsync([new RoomFeatureTextFilter(operation, value)]);
				(await ids.ToListAsync()).ShouldBe([Guid.Parse("10000000-0000-0000-0000-000000000001")]);
			}
			(await (await features.GetMatchingRoomIdsAsync([new RoomFeatureTextFilter(RoomFeatureTextOperator.Contains, "Wi-Fi' OR TRUE --")])).ToListAsync()).ShouldBeEmpty();
			foreach (var sql in generator.Generate(migration.DownOperations, migration.TargetModel)) await ExecuteAsync(connection, sql.CommandText);
			(await ScalarAsync<long>(connection, "SELECT count(*) FROM \"academic_Divisions\"")).ShouldBe(4);
			(await ScalarAsync<long>(connection, "SELECT count(*) FROM \"AppInstituteReportJobs\"")).ShouldBe(2);
			(await ScalarAsync<string>(connection, "SELECT \"Value\" FROM \"UnrelatedSentinel\"")).ShouldBe("preserved");
		}
		finally
		{
			if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^institute_perf_tests_[0-9a-f]{32}$")) throw new InvalidOperationException("Unsafe fixture database name.");
			await ExecuteAsync(admin, $"DROP DATABASE \"{name}\" WITH (FORCE)");
		}
	}

	private sealed class MigrationQueryContext(DbContextOptions<ShikshaMitraDbContext> options) : ShikshaMitraDbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);
			builder.Entity<Room>().HasQueryFilter(room => true);
		}
	}

	private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}
	private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		return (T)(await command.ExecuteScalarAsync())!;
	}
}
