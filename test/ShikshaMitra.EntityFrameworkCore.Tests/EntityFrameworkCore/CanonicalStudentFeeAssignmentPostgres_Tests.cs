using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public sealed class FinanceMigrationPostgresFactAttribute : FactAttribute
{
	public FinanceMigrationPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FINANCE_MIGRATION_TEST_POSTGRES")))
			Skip = "Requires a separate empty disposable loopback payroll_tests_* database in FINANCE_MIGRATION_TEST_POSTGRES.";
	}
}

public class CanonicalStudentFeeAssignmentPostgres_Tests
{
	private const string PreviousMigration = "20260913070559_AddDurablePayrollPreviews";
	private const string AssignmentMigration = "20260915122353_canonicalFeeAssignment";
	private const string AssignmentId = "c0353d11-6bd6-43e2-a945-9241f14f5db1";
	private const string DueId = "08c16064-f026-422f-a7e1-70515a75e988";

	[Fact]
	public void Latest_Migration_Snapshot_Matches_The_Current_Model()
	{
		// Match the design-time factory; CI runs this separately from ABP-starting fixtures.
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Database=payroll_tests_model_only").Options);
		var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
		var previous = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
		var current = db.GetService<IDesignTimeModel>().Model;
		var differences = db.GetService<IMigrationsModelDiffer>()
			.GetDifferences(previous.GetRelationalModel(), current.GetRelationalModel());
		var pendingSql = db.GetService<IMigrationsSqlGenerator>().Generate(differences, current);
		differences.ShouldBeEmpty(string.Join(Environment.NewLine, pendingSql.Select(command => command.CommandText)));
		db.Database.HasPendingModelChanges().ShouldBeFalse();
	}

	[FinanceMigrationPostgresFact]
	public async Task Upgrade_Preserves_Legacy_Records_And_Rollback_Protects_Each_New_Evidence_Field()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FINANCE_MIGRATION_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Migration tests require an empty disposable loopback payroll_tests_* database.");
		await using var sql = new NpgsqlConnection(connection.ConnectionString);
		await sql.OpenAsync();
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')"))
			.ShouldBe(0, "Never reset or reuse an existing database for migration tests.");

		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection.ConnectionString).Options);
		var migrator = db.GetService<IMigrator>();
		await migrator.MigrateAsync(PreviousMigration);
		await ExecuteAsync(sql, $$"""
			INSERT INTO "Finance_StudentFeeStructures"
			("Id", "AcademicYearId", "StudentId", "FeeStructureStandardId", "ConcessionPolicyId", "ConcessionPolicyVersionId",
			 "ConcessionApplicableFeeHeadIdsJson", "ConcessionFeeHeadScope", "ChargeStartDate", "ChargeEndDate", "ChargePeriodSource",
			 "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{AssignmentId}}', gen_random_uuid(), gen_random_uuid(), gen_random_uuid(),
			 '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222',
			 '[]', 0, '2026-04-01Z', '2027-03-31Z', 0, '{}', 'synthetic-migration-fixture', '2026-09-01Z');
			INSERT INTO "Finance_DueFees"
			("Id", "StudentId", "FeeHeadId", "Month", "Year", "IsOneTime", "DueDate", "OriginalAmount", "NetAmount",
			 "PaidAmount", "OutstandingAmount", "RefundedAmount", "AdjustmentCreditAmount", "ConcessionAmount", "ScheduledAmount",
			 "Status", "ScheduleSource", "ScheduleInstanceKey", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('{{DueId}}', gen_random_uuid(), gen_random_uuid(), 9, 2026, false, '2026-09-15Z',
			 1234.56, 1134.56, 234.56, 900, 0, 0, 100, 1234.56, 0, 0, 'synthetic-migration-due', '{}', 'synthetic-due', '2026-09-01Z');
			""");
		var beforeAssignment = await ScalarAsync<string>(sql, "SELECT to_jsonb(a)::text FROM \"Finance_StudentFeeStructures\" a");
		var beforeDue = await ScalarAsync<string>(sql, "SELECT to_jsonb(d)::text FROM \"Finance_DueFees\" d");

		await migrator.MigrateAsync(AssignmentMigration);
		(await ScalarAsync<string>(sql, "SELECT (to_jsonb(a) - 'AssignmentSnapshotJson')::text FROM \"Finance_StudentFeeStructures\" a"))
			.ShouldBe(beforeAssignment);
		(await ScalarAsync<string>(sql, "SELECT (to_jsonb(d) - 'StudentFeeStructureId' - 'AllocationPercentage')::text FROM \"Finance_DueFees\" d"))
			.ShouldBe(beforeDue);
		(await ScalarAsync<bool>(sql, "SELECT \"AssignmentSnapshotJson\" IS NULL FROM \"Finance_StudentFeeStructures\"" )).ShouldBeTrue();
		(await ScalarAsync<bool>(sql, "SELECT \"StudentFeeStructureId\" IS NULL AND \"AllocationPercentage\" IS NULL FROM \"Finance_DueFees\"" )).ShouldBeTrue();
		db.Database.HasPendingModelChanges().ShouldBeFalse();

		// A legacy-only database can roll back without changing original rows.
		await migrator.MigrateAsync(PreviousMigration);
		(await ScalarAsync<string>(sql, "SELECT to_jsonb(a)::text FROM \"Finance_StudentFeeStructures\" a")).ShouldBe(beforeAssignment);
		(await ScalarAsync<string>(sql, "SELECT to_jsonb(d)::text FROM \"Finance_DueFees\" d")).ShouldBe(beforeDue);
		await migrator.MigrateAsync(AssignmentMigration);

		foreach (var mutation in new[]
		{
			"UPDATE \"Finance_StudentFeeStructures\" SET \"AssignmentSnapshotJson\" = '{\"synthetic\":true}'",
			"UPDATE \"Finance_StudentFeeStructures\" SET \"ConcessionPolicyId\" = NULL",
			"UPDATE \"Finance_StudentFeeStructures\" SET \"ConcessionPolicyVersionId\" = NULL",
			$"UPDATE \"Finance_DueFees\" SET \"StudentFeeStructureId\" = '{AssignmentId}'",
			"UPDATE \"Finance_DueFees\" SET \"AllocationPercentage\" = 33.3333"
		})
		{
			await ExecuteAsync(sql, mutation);
			var assignmentEvidence = await ScalarAsync<string>(sql, "SELECT to_jsonb(a)::text FROM \"Finance_StudentFeeStructures\" a");
			var dueEvidence = await ScalarAsync<string>(sql, "SELECT to_jsonb(d)::text FROM \"Finance_DueFees\" d");
			var rejected = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(PreviousMigration));
			rejected.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
			rejected.MessageText.ShouldContain("use a forward fix");
			(await db.Database.GetAppliedMigrationsAsync()).ShouldContain(AssignmentMigration);
			(await ScalarAsync<string>(sql, "SELECT to_jsonb(a)::text FROM \"Finance_StudentFeeStructures\" a")).ShouldBe(assignmentEvidence);
			(await ScalarAsync<string>(sql, "SELECT to_jsonb(d)::text FROM \"Finance_DueFees\" d")).ShouldBe(dueEvidence);
			await ExecuteAsync(sql, """
				UPDATE "Finance_StudentFeeStructures" SET "AssignmentSnapshotJson" = NULL,
				 "ConcessionPolicyId" = '11111111-1111-1111-1111-111111111111', "ConcessionPolicyVersionId" = '22222222-2222-2222-2222-222222222222';
				UPDATE "Finance_DueFees" SET "StudentFeeStructureId" = NULL, "AllocationPercentage" = NULL;
				""");
		}

		var orphan = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql,
			"UPDATE \"Finance_DueFees\" SET \"StudentFeeStructureId\" = '33333333-3333-3333-3333-333333333333'"));
		orphan.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
		await ExecuteAsync(sql, $"UPDATE \"Finance_DueFees\" SET \"StudentFeeStructureId\" = '{AssignmentId}', \"AllocationPercentage\" = 33.333333");
		(await ScalarAsync<decimal>(sql, "SELECT \"AllocationPercentage\" FROM \"Finance_DueFees\"")).ShouldBe(33.333333m);
		var delete = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(sql,
			$"DELETE FROM \"Finance_StudentFeeStructures\" WHERE \"Id\" = '{AssignmentId}'"));
		delete.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
		delete.ConstraintName.ShouldBe("FK_DueFees_StudentFeeAssignment");
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_Finance_DueFees_StudentFeeStructureId'")).ShouldBe(1);
		(await ScalarAsync<decimal>(sql, "SELECT \"PaidAmount\" FROM \"Finance_DueFees\"")).ShouldBe(234.56m);
		await ExecuteAsync(sql, "UPDATE \"Finance_StudentFeeStructures\" SET \"ConcessionPolicyId\" = NULL, \"ConcessionPolicyVersionId\" = NULL");
		await migrator.MigrateAsync();
		db.Database.HasPendingModelChanges().ShouldBeFalse();
		(await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
		(await ScalarAsync<bool>(sql, "SELECT \"ConcessionPolicyId\" IS NULL AND \"ConcessionPolicyVersionId\" IS NULL FROM \"Finance_StudentFeeStructures\"")).ShouldBeTrue();
	}

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
