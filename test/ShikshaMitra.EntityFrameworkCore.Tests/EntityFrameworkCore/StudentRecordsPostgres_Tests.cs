using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public sealed class StudentRecordsPostgresFactAttribute : FactAttribute
{
	public StudentRecordsPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STUDENT_RECORDS_TEST_POSTGRES")))
			Skip = "Requires a new empty loopback records_tests_* database in STUDENT_RECORDS_TEST_POSTGRES.";
	}
}

/// <summary>Actual-provider migration, concurrent command uniqueness, and atomic rollback evidence.</summary>
public class StudentRecordsPostgres_Tests
{
	private const string PreviousMigration = "20260928183735_AddStudentPortalAccountLinks";
	private const string RecordsMigration = "20260930175531_AddStudentRecordGovernanceAndOperations";
	private const string TargetScopeMigration = "20260930182702_AddStudentRecordTransitionTargetScope";
	private const string LatestRecordsMigration = "20261001021100_AddStudentRecordRenewalHistory";
	private const string TenantId = "6351e61e-937c-4a7a-a5c2-352f04a7d3fb";
	private const string StudentId = "2f529edb-604b-4912-a86c-e4ba9a118a3f";
	private const string YearId = "a5b1f449-cf24-4662-ae11-9e5ad8dd7023";
	private const string BoardId = "c919bf71-25c1-4194-b8de-7c3b5914a90b";
	private const string MediumId = "c919bf71-25c1-4194-b8de-7c3b5914a90c";
	private const string ChangeId = "cb46e835-23a4-4ed0-96ba-cd4f57b89b86";

	[StudentRecordsPostgresFact]
	public async Task Record_postgres_migration_preserves_identity_concurrent_commands_and_rollback_evidence()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDENT_RECORDS_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("records_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Only a new empty loopback records_tests_* database is allowed.");
		await using var sql = new NpgsqlConnection(connection.ConnectionString);
		await sql.OpenAsync();
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')"))
			.ShouldBe(0, "Do not reset or reuse a populated database.");

		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection.ConnectionString).Options);
		var migrator = db.GetService<IMigrator>();
		await migrator.MigrateAsync(PreviousMigration);
		await SeedIdentityAsync(sql);
		var before = await IdentitySnapshotAsync(sql);
		await migrator.MigrateAsync(LatestRecordsMigration);
		(await IdentitySnapshotAsync(sql)).ShouldBe(before);
		(await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
		// Other work in this shared checkout can have pending models; validate the Records predicate explicitly.
		db.Model.FindEntityType(typeof(StudentRecordRenewalCase))!.GetIndexes()
			.Single(x => string.Join(",", x.Properties.Select(p => p.Name)) == "TenantId,StudentId,AcademicYearId")
			.GetFilter().ShouldBe("\"IsDeleted\" = false AND \"Status\" NOT IN ('Completed','Cancelled')");
		foreach (var aggregate in new[] { typeof(StudentRecordChange), typeof(StudentRecordTransition), typeof(StudentRecordRenewalCase) })
			db.Model.FindEntityType(aggregate)!.FindProperty("ConcurrencyStamp")!.IsConcurrencyToken.ShouldBeTrue();
		await VerifyRenewalHistoryAndSerialAllocationAsync(sql, migrator);

		// A failed transaction must commit neither the permanent identity change nor its governed request.
		await using (var transaction = await sql.BeginTransactionAsync(IsolationLevel.Serializable))
		{
			await ExecuteAsync(sql, $"UPDATE \"AppStudents\" SET \"FirstName\" = 'Uncommitted' WHERE \"Id\" = '{StudentId}'", transaction);
			await InsertChangeAsync(sql, ChangeId, "rollback-fixture", transaction);
			var invalid = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql,
				$"UPDATE \"AppStudentRecordChanges\" SET \"StudentId\" = '{Guid.NewGuid()}' WHERE \"Id\" = '{ChangeId}'", transaction));
			invalid.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
			await transaction.RollbackAsync();
		}
		(await IdentitySnapshotAsync(sql)).ShouldBe(before);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordChanges\"" )).ShouldBe(0);

		// A second request waits for the first transaction and is rejected by the real idempotency index.
		await using var concurrent = new NpgsqlConnection(connection.ConnectionString);
		await concurrent.OpenAsync();
		await using (var first = await sql.BeginTransactionAsync())
		{
			await InsertChangeAsync(sql, ChangeId, "concurrent-fixture", first);
			var second = InsertChangeAsync(concurrent, Guid.NewGuid().ToString(), "concurrent-fixture");
			await first.CommitAsync();
			var duplicate = await Should.ThrowAsync<PostgresException>(() => second);
			duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		}
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordChanges\"" )).ShouldBe(1);

		// A stale writer cannot overwrite a committed decision when matching the captured concurrency stamp.
		var updated = await ExecuteAsync(sql, $"UPDATE \"AppStudentRecordChanges\" SET \"Status\" = 'Scheduled', \"ConcurrencyStamp\" = 'committed' WHERE \"Id\" = '{ChangeId}' AND \"ConcurrencyStamp\" = 'synthetic-change'");
		updated.ShouldBe(1);
		(await ExecuteAsync(concurrent, $"UPDATE \"AppStudentRecordChanges\" SET \"Status\" = 'Cancelled' WHERE \"Id\" = '{ChangeId}' AND \"ConcurrencyStamp\" = 'synthetic-change'")).ShouldBe(0);
		(await ScalarAsync<string>(sql, $"SELECT \"Status\" FROM \"AppStudentRecordChanges\" WHERE \"Id\" = '{ChangeId}'")).ShouldBe("Scheduled");

		var rejected = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(PreviousMigration));
		rejected.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
		rejected.MessageText.ShouldContain("forward fix");
		(await db.Database.GetAppliedMigrationsAsync()).ShouldContain(RecordsMigration);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordChanges\"" )).ShouldBe(1);
		(await IdentitySnapshotAsync(sql)).ShouldBe(before);

		// This database contains only synthetic fixtures; deleting that fixture permits an unused-feature rollback.
		await ExecuteAsync(sql, "DELETE FROM \"AppStudentRecordChanges\"");
		await migrator.MigrateAsync(PreviousMigration);
		(await IdentitySnapshotAsync(sql)).ShouldBe(before);
		(await db.Database.GetAppliedMigrationsAsync()).ShouldNotContain(RecordsMigration);
		await migrator.MigrateAsync(LatestRecordsMigration);
		(await IdentitySnapshotAsync(sql)).ShouldBe(before);
	}

	private static async Task SeedIdentityAsync(NpgsqlConnection sql)
	{
		await ExecuteAsync(sql, $$"""
		INSERT INTO "academic_Years"
		("Id", "TenantId", "Name", "StartDate", "EndDate", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
		VALUES ('{{YearId}}', '{{TenantId}}', 'Synthetic Records year', '2026-04-01Z', '2027-03-31Z', '{}', 'synthetic-year', '2026-09-01Z');
		INSERT INTO "academic_EducationBoards"
		("Id", "TenantId", "Type", "StartDate", "EndDate", "AcademicYearId", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
		VALUES ('{{BoardId}}', '{{TenantId}}', 0, '2026-04-01Z', '2027-03-31Z', '{{YearId}}', '{}', 'synthetic-board', '2026-09-01Z');
		INSERT INTO "academic_EducationMediums"
		("Id", "TenantId", "Name", "EducationBoardId", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
		VALUES ('{{MediumId}}', '{{TenantId}}', 'Synthetic English', '{{BoardId}}', '{}', 'synthetic-medium', '2026-09-01Z');
		INSERT INTO "AppStudents"
		("Id", "TenantId", "FirstName", "LastName", "AcademicMediumId", "Status", "GrNumber", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
		VALUES ('{{StudentId}}', '{{TenantId}}', 'Permanent', 'Student', '{{MediumId}}', 0, 'SYNTHETIC-RECORDS-1', '{}', 'synthetic-student', '2026-09-01Z');
		""");
	}

	private static Task<int> InsertChangeAsync(NpgsqlConnection sql, string id, string key, NpgsqlTransaction? transaction = null) =>
		ExecuteAsync(sql, $$"""
			INSERT INTO "AppStudentRecordChanges"
			("Id", "TenantId", "StudentId", "AcademicYearId", "RequestedBy", "Section", "Action", "ClientKey", "Reason",
			 "BeforeJson", "PayloadJson", "PolicyJson", "ApprovalStagesJson", "Status", "ApprovalCount", "RequiredApprovals", "EffectiveAt",
			 "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{id}}', '{{TenantId}}', '{{StudentId}}', '{{YearId}}', gen_random_uuid(), 'communication', 'Contact', '{{key}}',
			 'Synthetic concurrency fixture', '{}', '{}', '{}', '[]', 'Approved', 0, 0, '2026-10-01Z', '{}', 'synthetic-change', '2026-10-01Z');
			""", transaction);

	private static async Task VerifyRenewalHistoryAndSerialAllocationAsync(NpgsqlConnection sql, IMigrator migrator)
	{
		// Closed reviews remain immutable while the active-only index allows another reviewed return.
		await InsertRenewalAsync(sql, Guid.NewGuid(), StudentId, "Completed", "history-one", 1);
		await InsertRenewalAsync(sql, Guid.NewGuid(), StudentId, "Completed", "history-two", 1);
		await InsertRenewalAsync(sql, Guid.NewGuid(), StudentId, "PendingChecklist", "active-one", 1);
		var duplicate = await Should.ThrowAsync<PostgresException>(() =>
			InsertRenewalAsync(sql, Guid.NewGuid(), StudentId, "PendingChecklist", "active-two", 1));
		duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordRenewalCases\"" )).ShouldBe(3);
		var unsafeDowngrade = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(TargetScopeMigration));
		unsafeDowngrade.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
		unsafeDowngrade.MessageText.ShouldContain("forward fix");
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordRenewalCases\"" )).ShouldBe(3);
		await ExecuteAsync(sql, "DELETE FROM \"AppStudentRecordRenewalCases\"");

		// The same predicate read used by allocation must prevent two serializable writers reserving one roll.
		var secondStudent = Guid.NewGuid().ToString();
		await ExecuteAsync(sql, $$"""
			INSERT INTO "AppStudents"
			("Id", "TenantId", "FirstName", "LastName", "AcademicMediumId", "Status", "GrNumber", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{secondStudent}}', '{{TenantId}}', 'Second', 'Synthetic', '{{MediumId}}', 0, 'SYNTHETIC-RECORDS-2', '{}', 'synthetic-second', '2026-09-01Z');
			""");
		await using var other = new NpgsqlConnection(sql.ConnectionString);
		await other.OpenAsync();
		await using var first = await sql.BeginTransactionAsync(IsolationLevel.Serializable);
		await using var second = await other.BeginTransactionAsync(IsolationLevel.Serializable);
		var predicate = $"SELECT count(*) FROM \"AppStudentRecordRenewalCases\" WHERE \"TenantId\" = '{TenantId}' AND \"AcademicYearId\" = '{YearId}' AND \"Status\" IN ('PendingChecklist','Provisioning')";
		(await ScalarAsync<long>(sql, predicate, first)).ShouldBe(0);
		(await ScalarAsync<long>(other, predicate, second)).ShouldBe(0);
		await InsertRenewalAsync(sql, Guid.NewGuid(), StudentId, "PendingChecklist", "serial-first", 1, first);
		await InsertRenewalAsync(other, Guid.NewGuid(), secondStudent, "PendingChecklist", "serial-second", 1, second);
		await first.CommitAsync();
		var serialization = await Should.ThrowAsync<PostgresException>(() => second.CommitAsync());
		serialization.SqlState.ShouldBe(PostgresErrorCodes.SerializationFailure);
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordRenewalCases\"" )).ShouldBe(1);
		await ExecuteAsync(sql, "DELETE FROM \"AppStudentRecordRenewalCases\"");
		await ExecuteAsync(sql, $"DELETE FROM \"AppStudents\" WHERE \"Id\" = '{secondStudent}'");
	}

	private static Task<int> InsertRenewalAsync(NpgsqlConnection sql, Guid id, string student, string status, string key, int roll, NpgsqlTransaction? transaction = null) =>
		ExecuteAsync(sql, $$"""
			INSERT INTO "AppStudentRecordRenewalCases"
			("Id", "TenantId", "StudentId", "AcademicYearId", "StandardId", "DivisionId", "RollNumber", "Status", "PolicyJson", "OnboardingRulesJson", "GuardianReviewConfirmed", "SourceSnapshotJson", "IdempotencyKey", "ExtraProperties", "ConcurrencyStamp", "CreationTime", "IsDeleted")
			VALUES ('{{id}}', '{{TenantId}}', '{{student}}', '{{YearId}}', '{{BoardId}}', '{{MediumId}}', {{roll}}, '{{status}}', '{}', '{}', false, '{}', '{{key}}', '{}', 'synthetic-renewal', '2026-10-01Z', false);
			""", transaction);

	private static Task<string> IdentitySnapshotAsync(NpgsqlConnection sql) =>
		ScalarAsync<string>(sql, $"SELECT to_jsonb(s)::text FROM \"AppStudents\" s WHERE \"Id\" = '{StudentId}'");

	private static async Task<int> ExecuteAsync(NpgsqlConnection sql, string text, NpgsqlTransaction? transaction = null)
	{
		await using var command = new NpgsqlCommand(text, sql, transaction);
		return await command.ExecuteNonQueryAsync();
	}
	private static async Task<T> ScalarAsync<T>(NpgsqlConnection sql, string text, NpgsqlTransaction? transaction = null)
	{
		await using var command = new NpgsqlCommand(text, sql, transaction);
		return (T)(await command.ExecuteScalarAsync())!;
	}
}
