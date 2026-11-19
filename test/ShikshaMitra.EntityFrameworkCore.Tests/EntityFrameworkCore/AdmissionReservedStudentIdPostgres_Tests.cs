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

public sealed class AdmissionReservationPostgresFactAttribute : FactAttribute
{
	public AdmissionReservationPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ADMISSION_RESERVATION_TEST_POSTGRES")))
			Skip = "Requires an empty disposable loopback admissions_tests_* database in ADMISSION_RESERVATION_TEST_POSTGRES.";
	}
}

public class AdmissionReservedStudentIdPostgres_Tests
{
	private const string PreviousMigration = "20260923155805_removePaymentTermCol";
	private const string ActiveApplicantId = "73f163b3-aa34-4285-b0a0-884337ddf040";
	private const string LinkedApplicantId = "73f163b3-aa34-4285-b0a0-884337ddf041";
	private const string DeletedApplicantId = "73f163b3-aa34-4285-b0a0-884337ddf042";
	private const string HostApplicantId = "73f163b3-aa34-4285-b0a0-884337ddf043";
	private const string ExistingStudentId = "fa8739ab-d7d0-4eec-be63-7da368e1d112";
	private const string TenantId = "463765d4-4d80-478c-b649-1d031ff00745";
	private const string YearId = "c234ac5d-c91a-4c4d-91d4-056c4083e371";
	private const string BoardId = "c234ac5d-c91a-4c4d-91d4-056c4083e372";
	private const string MediumId = "c234ac5d-c91a-4c4d-91d4-056c4083e373";

	[AdmissionReservationPostgresFact]
	public async Task Upgrade_preserves_legacy_identity_and_reserves_unique_ids_without_creating_Students()
	{
		var connection = new NpgsqlConnectionStringBuilder(
			Environment.GetEnvironmentVariable("ADMISSION_RESERVATION_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("admissions_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Migration tests require an empty disposable loopback admissions_tests_* database.");
		await using var sql = new NpgsqlConnection(connection.ConnectionString);
		await sql.OpenAsync();
		(await ScalarAsync<long>(sql,
			"SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')"))
			.ShouldBe(0, "Never reset or reuse an existing database for migration tests.");

		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection.ConnectionString).Options);
		var migration = db.Database.GetMigrations().Single(name =>
			name.EndsWith("_AddAdmissionApplicantReservedStudentId", StringComparison.Ordinal));
		var migrator = db.GetService<IMigrator>();
		await migrator.MigrateAsync(PreviousMigration);
		await SeedLegacyIdentityAsync(sql);
		var applicantsBefore = await ApplicantSnapshotAsync(sql, excludeReservation: false);
		var studentsBefore = await ScalarAsync<string>(sql,
			"SELECT jsonb_agg(to_jsonb(s) ORDER BY s.\"Id\")::text FROM \"AppStudents\" s");

		await migrator.MigrateAsync(migration);
		(await ApplicantSnapshotAsync(sql, excludeReservation: true)).ShouldBe(applicantsBefore);
		(await ScalarAsync<string>(sql,
			"SELECT jsonb_agg(to_jsonb(s) ORDER BY s.\"Id\")::text FROM \"AppStudents\" s")).ShouldBe(studentsBefore);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppAdmissionApplicants\"" )).ShouldBe(4);
		(await ScalarAsync<long>(sql,
			"SELECT count(DISTINCT \"ReservedStudentId\") FROM \"AppAdmissionApplicants\"" )).ShouldBe(4);
		(await ScalarAsync<bool>(sql, """
			SELECT bool_and("ReservedStudentId" IS NOT NULL AND
			    "ReservedStudentId" <> '00000000-0000-0000-0000-000000000000'::uuid)
			FROM "AppAdmissionApplicants"
			""")).ShouldBeTrue();
		(await ScalarAsync<long>(sql, """
			SELECT count(*) FROM "AppAdmissionApplicants" a
			JOIN "AppStudents" s ON s."Id" = a."ReservedStudentId"
			""")).ShouldBe(0, "Backfilling reservations must not create or reassign actual Students.");
		(await ScalarAsync<Guid>(sql,
			$"SELECT \"ExistingStudentId\" FROM \"AppAdmissionApplicants\" WHERE \"Id\" = '{LinkedApplicantId}'"))
			.ShouldBe(Guid.Parse(ExistingStudentId));
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentOnboardingCases\"" )).ShouldBe(0);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentEnrollments\"" )).ShouldBe(0);
		(await ScalarAsync<bool>(sql, """
			SELECT is_nullable = 'NO' AND column_default IS NULL AND udt_name = 'uuid'
			FROM information_schema.columns WHERE table_schema = 'public'
			  AND table_name = 'AppAdmissionApplicants' AND column_name = 'ReservedStudentId'
			""")).ShouldBeTrue();
		(await ScalarAsync<long>(sql, """
			SELECT count(*) FROM pg_constraint c
			JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = ANY(c.conkey)
			WHERE c.conrelid = '"AppAdmissionApplicants"'::regclass AND c.contype = 'f'
			  AND a.attname = 'ReservedStudentId'
			""")).ShouldBe(0);

		var reservation = await ScalarAsync<Guid>(sql,
			$"SELECT \"ReservedStudentId\" FROM \"AppAdmissionApplicants\" WHERE \"Id\" = '{ActiveApplicantId}'");
		foreach (var applicantId in new[] { LinkedApplicantId, DeletedApplicantId, HostApplicantId })
		{
			var duplicate = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql,
				$"UPDATE \"AppAdmissionApplicants\" SET \"ReservedStudentId\" = '{reservation}' WHERE \"Id\" = '{applicantId}'"));
			duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
			duplicate.ConstraintName.ShouldBe("UX_AdmissionApplicants_ReservedStudentId");
		}
		var missing = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql,
			$"UPDATE \"AppAdmissionApplicants\" SET \"ReservedStudentId\" = NULL WHERE \"Id\" = '{ActiveApplicantId}'"));
		missing.SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);
		var empty = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql,
			$"UPDATE \"AppAdmissionApplicants\" SET \"ReservedStudentId\" = '{Guid.Empty}' WHERE \"Id\" = '{ActiveApplicantId}'"));
		empty.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
		empty.ConstraintName.ShouldBe("CK_AdmissionApplicants_ReservedStudentId");

		// Re-running the migration command must not regenerate any reserved identity.
		var reservations = await ApplicantSnapshotAsync(sql, excludeReservation: false);
		await migrator.MigrateAsync(migration);
		(await ApplicantSnapshotAsync(sql, excludeReservation: false)).ShouldBe(reservations);
		db.Database.HasPendingModelChanges().ShouldBeFalse();
	}

	private static async Task SeedLegacyIdentityAsync(NpgsqlConnection sql)
	{
		// A real historical Student proves that its primary key and applicant link survive unchanged.
		await ExecuteAsync(sql, $$"""
			INSERT INTO "academic_Years"
			("Id", "TenantId", "Name", "StartDate", "EndDate", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{YearId}}', '{{TenantId}}', 'Synthetic 2026-27', '2026-04-01Z', '2027-03-31Z', '{}', 'synthetic-year', '2026-09-01Z');
			INSERT INTO "academic_EducationBoards"
			("Id", "TenantId", "Type", "StartDate", "EndDate", "AcademicYearId", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{BoardId}}', '{{TenantId}}', 0, '2026-04-01Z', '2027-03-31Z', '{{YearId}}', '{}', 'synthetic-board', '2026-09-01Z');
			INSERT INTO "academic_EducationMediums"
			("Id", "TenantId", "Name", "EducationBoardId", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{MediumId}}', '{{TenantId}}', 'Synthetic English', '{{BoardId}}', '{}', 'synthetic-medium', '2026-09-01Z');
			INSERT INTO "AppStudents"
			("Id", "TenantId", "FirstName", "LastName", "AcademicMediumId", "Status", "GrNumber", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{ExistingStudentId}}', '{{TenantId}}', 'Historical', 'Student', '{{MediumId}}', 0, 'SYNTHETIC-001', '{}', 'synthetic-student', '2026-09-01Z');
			INSERT INTO "AppAdmissionApplicants"
			("Id", "TenantId", "FamilyId", "FirstName", "LastName", "ExistingStudentId", "Status", "IsDeleted",
			 "DeletionTime", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES
			('{{ActiveApplicantId}}', '{{TenantId}}', gen_random_uuid(), 'Active', 'Applicant', NULL, 0, false,
			 NULL, '{"fixture":"active"}', 'synthetic-active', '2026-09-01Z'),
			('{{LinkedApplicantId}}', '{{TenantId}}', gen_random_uuid(), 'Linked', 'Applicant', '{{ExistingStudentId}}', 0, false,
			 NULL, '{"fixture":"linked"}', 'synthetic-linked', '2026-09-02Z'),
			('{{DeletedApplicantId}}', '754c737c-27ec-4bf9-ae2f-d96d994d5dc8', gen_random_uuid(), 'Deleted', 'Applicant', NULL, 0, true,
			 '2026-09-04Z', '{"fixture":"deleted"}', 'synthetic-deleted', '2026-09-03Z'),
			('{{HostApplicantId}}', NULL, gen_random_uuid(), 'Host', 'Applicant', NULL, 0, false,
			 NULL, '{"fixture":"host"}', 'synthetic-host', '2026-09-04Z');
			""");
	}

	private static Task<string> ApplicantSnapshotAsync(NpgsqlConnection connection, bool excludeReservation) =>
		ScalarAsync<string>(connection, excludeReservation
			? "SELECT jsonb_agg(to_jsonb(a) - 'ReservedStudentId' ORDER BY a.\"Id\")::text FROM \"AppAdmissionApplicants\" a"
			: "SELECT jsonb_agg(to_jsonb(a) ORDER BY a.\"Id\")::text FROM \"AppAdmissionApplicants\" a");

	private static async Task ExecuteAsync(NpgsqlConnection connection, string text)
	{
		await using var command = new NpgsqlCommand(text, connection);
		await command.ExecuteNonQueryAsync();
	}

	private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string text)
	{
		await using var command = new NpgsqlCommand(text, connection);
		return (T)(await command.ExecuteScalarAsync())!;
	}
}
