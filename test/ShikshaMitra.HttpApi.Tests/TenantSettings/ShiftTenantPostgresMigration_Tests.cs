using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public sealed class ShiftPostgresFactAttribute : FactAttribute
{
	public ShiftPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHIFT_TEST_POSTGRES")))
			Skip = "Requires a new disposable loopback shift_isolation_tests_* database in SHIFT_TEST_POSTGRES.";
	}
}

public class ShiftTenantPostgresMigration_Tests
{
	[ShiftPostgresFact, Trait("Module", "TenantSettings"), Trait("Tier", "Critical")]
	public async Task Migration_should_preserve_legacy_data_and_unrelated_tables_on_postgres()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SHIFT_TEST_POSTGRES"));
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^shift_isolation_tests_[a-z0-9_]{1,40}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Only a new disposable loopback shift_isolation_tests_* database is allowed.");
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres" }.ConnectionString);
		await admin.OpenAsync();
		await using var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin);
		await create.ExecuteNonQueryAsync();
		try
		{
			await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
			await db.Database.ExecuteSqlRawAsync("""
				CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) NOT NULL PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
				CREATE TABLE "academic_ShiftManagements" ("Id" uuid PRIMARY KEY, "ShiftName" text NOT NULL);
				INSERT INTO "academic_ShiftManagements" VALUES ('11111111-1111-1111-1111-111111111111', 'Legacy DDVS shift');
				CREATE TABLE "AppAcademicTerms" ("Sentinel" text);
				CREATE TABLE "AppStudentManagementSettings" ("Sentinel" text);
				CREATE TABLE "AppStudentRollNumberAssignments" ("Sentinel" text);
				INSERT INTO "AppAcademicTerms" VALUES ('preserve');
				INSERT INTO "AppStudentManagementSettings" VALUES ('preserve');
				INSERT INTO "AppStudentRollNumberAssignments" VALUES ('preserve');
				""");
			var migrations = db.GetService<IMigrationsAssembly>().Migrations;
			const string id = "20261014063540_AddShiftTenantIsolation";
			var predecessor = migrations.Keys.OrderBy(x => x).Last(x => string.CompareOrdinal(x, id) < 0);
			var migrator = db.GetService<IMigrator>();
			var script = migrator.GenerateScript(predecessor, id);
			script.ShouldNotContain("DROP TABLE");
			await db.Database.ExecuteSqlRawAsync(script);
			await using var sql = new NpgsqlConnection(connection.ConnectionString);
			await sql.OpenAsync();
			await using var legacy = new NpgsqlCommand("SELECT count(*) FROM \"academic_ShiftManagements\" WHERE \"TenantId\" IS NULL AND \"ShiftName\" = 'Legacy DDVS shift'", sql);
			((long)(await legacy.ExecuteScalarAsync())!).ShouldBe(1);
			await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(id, predecessor));
			await using var retained = new NpgsqlCommand("SELECT count(*) FROM \"academic_ShiftManagements\" WHERE \"ShiftName\" = 'Legacy DDVS shift'", sql);
			((long)(await retained.ExecuteScalarAsync())!).ShouldBe(1);
			foreach (var table in new[] { "AppAcademicTerms", "AppStudentManagementSettings", "AppStudentRollNumberAssignments" })
			{
				await using var sentinel = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\" WHERE \"Sentinel\" = 'preserve'", sql);
				((long)(await sentinel.ExecuteScalarAsync())!).ShouldBe(1);
			}
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
			await drop.ExecuteNonQueryAsync();
		}
	}
}
