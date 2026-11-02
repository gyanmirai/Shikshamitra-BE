using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Xunit;
namespace ShikshaMitra.HR;
public class EmployeeWorkShiftPostgres_Tests
{
	[ShiftPostgresFact, Trait("Module", "HR"), Trait("Tier", "Critical")]
	public async Task Migration_copies_definitions_and_snapshots_preserves_school_data_and_rejects_overlap()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SHIFT_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || !Regex.IsMatch(connection.Database ?? "", "^shift_isolation_tests_[a-z0-9_]{1,40}$")) throw new InvalidOperationException("Requires a new disposable loopback database.");
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres" }.ConnectionString); await admin.OpenAsync();
		await new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin).ExecuteNonQueryAsync();
		try
		{
			await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
			await db.Database.ExecuteSqlRawAsync("""
    CREATE TABLE "academic_ShiftManagements" ("Id" uuid PRIMARY KEY,"TenantId" uuid NULL,"ShiftName" text NOT NULL,"StartTime" time NOT NULL,"EndTime" time NOT NULL);
    CREATE TABLE "AppEmployees" ("Id" uuid PRIMARY KEY,"TenantId" uuid NULL,"ShiftId" uuid NULL,"DateOfJoining" date NULL, CONSTRAINT "FK_AppEmployees_academic_ShiftManagements_ShiftId" FOREIGN KEY("ShiftId") REFERENCES "academic_ShiftManagements"("Id"));
    CREATE TABLE "AppEmployeeAttendances" ("EmployeeId" uuid,"AttendanceDate" date,"Sentinel" text);
    CREATE TABLE "AppStandards" ("Id" uuid PRIMARY KEY,"ShiftId" uuid);
    CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY,"ProductVersion" varchar(32) NOT NULL);
    INSERT INTO "academic_ShiftManagements" VALUES ('11111111-1111-1111-1111-111111111111','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Shared shift','08:00','16:00'),('22222222-2222-2222-2222-222222222222','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','Unassigned shift','09:00','17:00'),('33333333-3333-3333-3333-333333333333',NULL,'Unresolved owner','09:00','17:00');
    INSERT INTO "AppEmployees" VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','11111111-1111-1111-1111-111111111111','2026-04-01');
    INSERT INTO "AppEmployeeAttendances" VALUES ('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb','2026-10-01','preserve');
    INSERT INTO "AppStandards" VALUES ('cccccccc-cccc-cccc-cccc-cccccccccccc','11111111-1111-1111-1111-111111111111');
    """);
			const string id = "20261102120944_SeparateEmployeeWorkShifts";
			var previous = db.GetService<IMigrationsAssembly>().Migrations.Keys.Order().Last(x => string.CompareOrdinal(x, id) < 0);
			var script = db.GetService<IMigrator>().GenerateScript(previous, id);
			script.ShouldNotContain("DROP TABLE");
			await db.Database.OpenConnectionAsync();
			var migrationConnection = (NpgsqlConnection) db.Database.GetDbConnection();
			// Reject foreign ownership before any schema or record change is committed.
			await new NpgsqlCommand("INSERT INTO \"AppEmployees\" VALUES ('dddddddd-dddd-dddd-dddd-dddddddddddd','eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee','11111111-1111-1111-1111-111111111111','2026-04-01')", migrationConnection).ExecuteNonQueryAsync();
			var invalid = await Should.ThrowAsync<PostgresException>(() => new NpgsqlCommand(script, migrationConnection).ExecuteNonQueryAsync());
			invalid.MessageText.ShouldContain("preflight failed");
			await new NpgsqlCommand("ROLLBACK; DELETE FROM \"AppEmployees\" WHERE \"Id\"='dddddddd-dddd-dddd-dddd-dddddddddddd'", migrationConnection).ExecuteNonQueryAsync();
			await new NpgsqlCommand(script, migrationConnection).ExecuteNonQueryAsync();
			await using var sql = new NpgsqlConnection(connection.ConnectionString); await sql.OpenAsync();
			async Task<long> Count(string query) => (long) (await new NpgsqlCommand(query, sql).ExecuteScalarAsync())!;
			(await Count("SELECT count(*) FROM \"HR_WorkShifts\"")).ShouldBe(2);
			(await Count("SELECT count(*) FROM \"HR_EmployeeShiftAssignments\" WHERE \"Provenance\"='ImportedLastKnown' AND \"StartTime\"='08:00'")).ShouldBe(1);
			(await Count("SELECT count(*) FROM \"academic_ShiftManagements\"")).ShouldBe(3);
			(await Count("SELECT count(*) FROM \"AppEmployeeAttendances\" WHERE \"Sentinel\"='preserve'")).ShouldBe(1);
			(await Count("SELECT count(*) FROM \"AppStandards\" WHERE \"ShiftId\"='11111111-1111-1111-1111-111111111111'")).ShouldBe(1);
			var duplicate = """
    INSERT INTO "HR_EmployeeShiftAssignments" SELECT md5(random()::text)::uuid,"TenantId","EmployeeId","WorkShiftId","EffectiveFrom","EffectiveTo","ShiftName","StartTime","EndTime","Provenance","ExtraProperties","ConcurrencyStamp","CreationTime","CreatorId","LastModificationTime","LastModifierId","IsDeleted","DeleterId","DeletionTime" FROM "HR_EmployeeShiftAssignments";
    """;
			var error = await Should.ThrowAsync<PostgresException>(() => new NpgsqlCommand(duplicate, sql).ExecuteNonQueryAsync()); error.SqlState.ShouldBe("23P01");
   // Exercise two writers against the exclusion constraint, including an uncommitted range.
   await new NpgsqlCommand("UPDATE \"HR_EmployeeShiftAssignments\" SET \"EffectiveTo\"=DATE '2026-12-01'",sql).ExecuteNonQueryAsync();
   var concurrentInsert=duplicate.Replace("\"EffectiveFrom\",\"EffectiveTo\"", "DATE '2026-12-01',NULL");
   await using var first=await sql.BeginTransactionAsync();
   await new NpgsqlCommand(concurrentInsert,sql,first).ExecuteNonQueryAsync();
   await using var other=new NpgsqlConnection(connection.ConnectionString);await other.OpenAsync();
   var competing=new NpgsqlCommand(concurrentInsert,other).ExecuteNonQueryAsync();
   await Task.Delay(100); competing.IsCompleted.ShouldBeFalse();
   await first.CommitAsync();
   var concurrentError=await Should.ThrowAsync<PostgresException>(async()=>await competing);concurrentError.SqlState.ShouldBe("23P01");
		}
		finally { NpgsqlConnection.ClearAllPools(); await new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin).ExecuteNonQueryAsync(); }
	}
}