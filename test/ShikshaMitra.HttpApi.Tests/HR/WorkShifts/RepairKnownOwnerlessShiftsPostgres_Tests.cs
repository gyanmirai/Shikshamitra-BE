using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Migrations;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR;

public class RepairKnownOwnerlessShiftsPostgres_Tests
{
	[ShiftPostgresFact, Trait("Module", "HR"), Trait("Tier", "Critical")]
	public async Task Migration_copies_verified_shared_shifts_per_tenant_before_work_shift_cutover()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SHIFT_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			!Regex.IsMatch(connection.Database ?? "", "^shift_isolation_tests_[a-z0-9_]{1,40}$"))
			throw new InvalidOperationException("Requires a disposable loopback database.");
		connection.Database = $"shift_isolation_tests_repair_{Guid.NewGuid():N}"[..38];

		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres" }.ConnectionString);
		await admin.OpenAsync();
		await new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin).ExecuteNonQueryAsync();
		try
		{
			await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
				.UseNpgsql(connection.ConnectionString).Options);
			await using var sql = new NpgsqlConnection(connection.ConnectionString);
			await sql.OpenAsync();
			await new NpgsqlCommand("""
				CREATE TABLE "academic_ShiftManagements" ("Id" uuid PRIMARY KEY, "TenantId" uuid NULL,
				  "ShiftName" text NOT NULL, "StartTime" time NOT NULL, "EndTime" time NOT NULL,
				  "ExtraProperties" text NOT NULL, "ConcurrencyStamp" varchar(40) NOT NULL);
				CREATE TABLE "AppEmployees" ("Id" uuid PRIMARY KEY, "TenantId" uuid NULL, "ShiftId" uuid NULL,
				  "DateOfJoining" date NULL, CONSTRAINT "FK_AppEmployees_academic_ShiftManagements_ShiftId"
				  FOREIGN KEY ("ShiftId") REFERENCES "academic_ShiftManagements"("Id"));
				CREATE TABLE "academic_Standards" ("Id" uuid PRIMARY KEY, "TenantId" uuid NULL, "ShiftId" uuid NULL,
				  CONSTRAINT "FK_academic_Standards_academic_ShiftManagements_ShiftId"
				  FOREIGN KEY ("ShiftId") REFERENCES "academic_ShiftManagements"("Id"));
				CREATE TABLE "AppEmployeeAttendances" ("EmployeeId" uuid, "AttendanceDate" date);
				CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
				INSERT INTO "academic_ShiftManagements" VALUES
				 ('3a2402e9-5c2b-b6b2-115e-9ba536bda72a',NULL,'Afternoon Shift','12:30','17:30','{}','old-afternoon'),
				 ('3a2402e9-5c2b-f11f-c723-987d258a8d09',NULL,'Full Day Shift','07:00','17:30','{}','old-full-day');
				INSERT INTO "AppEmployees"
				 SELECT gen_random_uuid(),'11111111-1111-1111-1111-111111111111',
				 '3a2402e9-5c2b-b6b2-115e-9ba536bda72a',DATE '2026-04-01' FROM generate_series(1,7);
				INSERT INTO "AppEmployees"
				 SELECT gen_random_uuid(),'11111111-1111-1111-1111-111111111111',
				 '3a2402e9-5c2b-f11f-c723-987d258a8d09',DATE '2026-04-01' FROM generate_series(1,2);
				INSERT INTO "academic_Standards" VALUES
				 (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','3a2402e9-5c2b-b6b2-115e-9ba536bda72a');
				INSERT INTO "academic_Standards"
				 SELECT gen_random_uuid(),'3a244aef-f540-277d-9cf5-9bd861f93b83',
				 '3a2402e9-5c2b-b6b2-115e-9ba536bda72a' FROM generate_series(1,15);
				""", sql).ExecuteNonQueryAsync();

			const string repairId = "20261102120943_RepairKnownOwnerlessShifts";
			const string cutoverId = "20261102120944_SeparateEmployeeWorkShifts";
			var migrator = db.GetService<IMigrator>();
			var migrations = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order().ToArray();
			migrations.ShouldContain(repairId);
			var previous = migrations.Last(id => string.CompareOrdinal(id, repairId) < 0);
			var repairScript = migrator.GenerateScript(previous, repairId);
			await new NpgsqlCommand("INSERT INTO \"academic_Standards\" VALUES (gen_random_uuid(),'ffffffff-ffff-ffff-ffff-ffffffffffff','3a2402e9-5c2b-b6b2-115e-9ba536bda72a')", sql).ExecuteNonQueryAsync();
			var changedOwnership = await Should.ThrowAsync<PostgresException>(() =>
				new NpgsqlCommand(repairScript, sql).ExecuteNonQueryAsync());
			changedOwnership.MessageText.ShouldContain("repair assumptions changed");
			await new NpgsqlCommand("ROLLBACK; DELETE FROM \"academic_Standards\" WHERE \"TenantId\"='ffffffff-ffff-ffff-ffff-ffffffffffff'", sql).ExecuteNonQueryAsync();
			await new NpgsqlCommand(repairScript, sql).ExecuteNonQueryAsync();
			async Task<long> Count(string query) => (long)(await new NpgsqlCommand(query, sql).ExecuteScalarAsync())!;
			(await Count("SELECT count(*) FROM \"academic_ShiftManagements\" WHERE \"TenantId\" IS NOT NULL")).ShouldBe(3);
			(await Count("SELECT count(*) FROM \"academic_ShiftManagements\" WHERE \"TenantId\" IS NULL")).ShouldBe(2);
			(await Count("SELECT count(*) FROM \"AppEmployees\" e JOIN \"academic_ShiftManagements\" s ON s.\"Id\"=e.\"ShiftId\" WHERE e.\"TenantId\"=s.\"TenantId\"")).ShouldBe(9);
			(await Count("SELECT count(*) FROM \"academic_Standards\" st JOIN \"academic_ShiftManagements\" s ON s.\"Id\"=st.\"ShiftId\" WHERE st.\"TenantId\"=s.\"TenantId\"")).ShouldBe(16);

			await new NpgsqlCommand(migrator.GenerateScript(repairId, cutoverId), sql).ExecuteNonQueryAsync();
			(await Count("SELECT count(*) FROM \"HR_EmployeeShiftAssignments\" WHERE \"Provenance\"='ImportedLastKnown'")).ShouldBe(9);
			(await Count("SELECT count(*) FROM \"HR_WorkShifts\"")).ShouldBe(3);
			var repairSql = new RepairKnownOwnerlessShifts().UpOperations.OfType<SqlOperation>().Single().Sql;
			await new NpgsqlCommand(repairSql, sql).ExecuteNonQueryAsync();
			(await Count("SELECT count(*) FROM \"academic_ShiftManagements\"")).ShouldBe(5);
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			await new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync();
		}
	}
}
