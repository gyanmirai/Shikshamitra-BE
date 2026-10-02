using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public sealed class StudentAttendancePostgresFactAttribute : FactAttribute
{
	public StudentAttendancePostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STUDENT_ATTENDANCE_TEST_POSTGRES")))
			Skip = "Requires a new empty loopback attendance_tests_* database in STUDENT_ATTENDANCE_TEST_POSTGRES.";
	}
}

public class StudentAttendancePostgres_Tests
{
	private const string PreviousMigration = "20261001021100_AddStudentRecordRenewalHistory";
	private const string AttendanceMigration = "20261001043143_AddUniversalStudentAttendance";
	private const string Tenant = "a1000000-0000-0000-0000-000000000001";
	private const string Year = "a1000000-0000-0000-0000-000000000002";
	private const string Board = "a1000000-0000-0000-0000-000000000003";
	private const string Medium = "a1000000-0000-0000-0000-000000000004";
	private const string Stage = "a1000000-0000-0000-0000-000000000005";
	private const string Standard = "a1000000-0000-0000-0000-000000000006";
	private const string Division = "a1000000-0000-0000-0000-000000000007";
	private const string OtherDivision = "a1000000-0000-0000-0000-000000000008";
	private const string Student = "a1000000-0000-0000-0000-000000000009";
	private const string Day = "a1000000-0000-0000-0000-000000000010";
	private const string OtherDay = "a1000000-0000-0000-0000-000000000011";

	[StudentAttendancePostgresFact]
	public async Task Attendance_migration_preserves_unknown_dates_and_conflicts_and_guards_used_feature_rollback()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDENT_ATTENDANCE_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("attendance_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Only a new empty loopback attendance_tests_* database is allowed.");
		await using var sql = new NpgsqlConnection(connection.ConnectionString);
		await sql.OpenAsync();
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')"))
			.ShouldBe(0, "Never reset or reuse an existing database.");
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
		var migrator = db.GetService<IMigrator>();
		await migrator.MigrateAsync(PreviousMigration);
		await SeedAsync(sql);
		var identity = await ScalarAsync<string>(sql, $"SELECT to_jsonb(s)::text FROM \"AppStudents\" s WHERE \"Id\" = '{Student}'");
		await migrator.MigrateAsync(AttendanceMigration);
		(await ScalarAsync<string>(sql, $"SELECT to_jsonb(s)::text FROM \"AppStudents\" s WHERE \"Id\" = '{Student}'")).ShouldBe(identity);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentEnrollments\" WHERE \"AttendanceStartDate\" IS NULL AND \"AttendancePlacementHistoryJson\" = '[]'" )).ShouldBe(1);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppAttendanceEntries\" WHERE \"SchoolDate\" = '2026-09-01' AND \"EnrollmentId\" IS NULL" )).ShouldBe(2);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppAttendanceEntries\" WHERE \"SchoolDate\" = '2026-09-02' AND \"EnrollmentId\" IS NOT NULL AND \"PresentCredit\" = 1 AND \"ExpectedAtSubmission\" IS NULL" )).ShouldBe(1);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppAttendanceDays\" WHERE \"IsSubmitted\" = false AND \"Revision\" = 0" )).ShouldBe(3);
		var enrollment = await ScalarAsync<Guid>(sql, "SELECT \"Id\" FROM \"AppStudentEnrollments\"");
		// Same school date cannot be recorded a second time in a different division.
		var duplicateDay = Guid.NewGuid();
		await ExecuteAsync(sql, $$"""
			INSERT INTO "AppAttendanceDays" ("Id","TenantId","AcademicYearId","StandardId","DivisionId","Date","MarkedByUserId","ExtraProperties","ConcurrencyStamp","CreationTime")
			VALUES ('{{duplicateDay}}','{{Tenant}}','{{Year}}','{{Standard}}','{{OtherDivision}}','2026-09-02',gen_random_uuid(),'{}','fixture',now());
			""");
		var duplicate = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql, $$"""
			INSERT INTO "AppAttendanceEntries" ("Id","TenantId","AttendanceDayId","StudentId","EnrollmentId","SchoolDate","Status","MarkedByUserId","CreationTime")
			VALUES (gen_random_uuid(),'{{Tenant}}','{{duplicateDay}}','{{Student}}','{{enrollment}}','2026-09-02','Present',gen_random_uuid(),now());
			"""));
		duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		duplicate.ConstraintName.ShouldBe("UX_AttendanceEntry_EnrollmentDate");
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppAttendanceEntries\"" )).ShouldBe(3);
		// An untouched upgrade may roll back without deleting the safely bridged enrollment.
		await migrator.MigrateAsync(PreviousMigration);
		(await ScalarAsync<Guid>(sql, "SELECT \"Id\" FROM \"AppStudentEnrollments\"")).ShouldBe(enrollment);
		await migrator.MigrateAsync(AttendanceMigration);
		await ExecuteAsync(sql, "UPDATE \"AppStudentEnrollments\" SET \"AttendanceStartDate\" = '2026-09-02'");
		var protectedRollback = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(PreviousMigration));
		protectedRollback.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
		protectedRollback.MessageText.ShouldContain("forward fix");
		(await db.Database.GetAppliedMigrationsAsync()).ShouldContain(AttendanceMigration);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppAttendanceEntries\"" )).ShouldBe(3);
		(await ScalarAsync<string>(sql, $"SELECT to_jsonb(s)::text FROM \"AppStudents\" s WHERE \"Id\" = '{Student}'")).ShouldBe(identity);
	}

	private static Task<int> SeedAsync(NpgsqlConnection sql) => ExecuteAsync(sql, $$"""
		INSERT INTO "academic_Years" ("Id","TenantId","Name","StartDate","EndDate","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Year}}','{{Tenant}}','Synthetic attendance year','2026-04-01Z','2027-03-31Z','{}','fixture',now());
		INSERT INTO "academic_EducationBoards" ("Id","TenantId","Type","StartDate","EndDate","AcademicYearId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Board}}','{{Tenant}}',0,'2026-04-01Z','2027-03-31Z','{{Year}}','{}','fixture',now());
		INSERT INTO "academic_EducationMediums" ("Id","TenantId","Name","EducationBoardId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Medium}}','{{Tenant}}','English','{{Board}}','{}','fixture',now());
		INSERT INTO "academic_EducationMediumStages" ("Id","TenantId","EducationMediumId","Name","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Stage}}','{{Tenant}}','{{Medium}}','Primary','{}','fixture',now());
		INSERT INTO "academic_Standards" ("Id","TenantId","EducationMediumStageId","Name","AgeLowerLimit","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Standard}}','{{Tenant}}','{{Stage}}','Class 1',5,'{}','fixture',now());
		INSERT INTO "academic_Divisions" ("Id","TenantId","StandardId","Name","Strength","RoomNumber","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Division}}','{{Tenant}}','{{Standard}}','A',30,'1','{}','fixture',now()), ('{{OtherDivision}}','{{Tenant}}','{{Standard}}','B',30,'2','{}','fixture',now());
		INSERT INTO "AppStudents" ("Id","TenantId","FirstName","LastName","AcademicBoardId","AcademicMediumId","AcademicYearId","StandardId","DivisionId","Status","RollNo","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Student}}','{{Tenant}}','Synthetic','Student','{{Board}}','{{Medium}}','{{Year}}','{{Standard}}','{{Division}}',6,1,'{}','fixture',now());
		INSERT INTO "AppAttendanceDays" ("Id","TenantId","AcademicYearId","StandardId","DivisionId","Date","MarkedByUserId","ExtraProperties","ConcurrencyStamp","CreationTime")
		VALUES ('{{Day}}','{{Tenant}}','{{Year}}','{{Standard}}','{{Division}}','2026-09-01',gen_random_uuid(),'{}','fixture',now()),
		 ('{{OtherDay}}','{{Tenant}}','{{Year}}','{{Standard}}','{{OtherDivision}}','2026-09-01',gen_random_uuid(),'{}','fixture',now()),
		 ('a1000000-0000-0000-0000-000000000012','{{Tenant}}','{{Year}}','{{Standard}}','{{Division}}','2026-09-02',gen_random_uuid(),'{}','fixture',now());
		INSERT INTO "AppAttendanceEntries" ("Id","TenantId","AttendanceDayId","StudentId","Status","MarkedByUserId","CreationTime")
		VALUES (gen_random_uuid(),'{{Tenant}}','{{Day}}','{{Student}}','Present',gen_random_uuid(),now()),
		 (gen_random_uuid(),'{{Tenant}}','{{OtherDay}}','{{Student}}','Absent',gen_random_uuid(),now()),
		 (gen_random_uuid(),'{{Tenant}}','a1000000-0000-0000-0000-000000000012','{{Student}}','Present',gen_random_uuid(),now());
		""");

	private static async Task<int> ExecuteAsync(NpgsqlConnection sql, string text)
	{
		await using var command = new NpgsqlCommand(text, sql);
		return await command.ExecuteNonQueryAsync();
	}
	private static async Task<T> ScalarAsync<T>(NpgsqlConnection sql, string text)
	{
		await using var command = new NpgsqlCommand(text, sql);
		return (T)(await command.ExecuteScalarAsync())!;
	}
}
