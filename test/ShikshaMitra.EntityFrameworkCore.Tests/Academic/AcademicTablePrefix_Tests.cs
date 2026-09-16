using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Academic;

public class AcademicTablePrefix_Tests
{
	private const string Provider = "Npgsql.EntityFrameworkCore.PostgreSQL";

	[Fact]
	public void Academic_And_Shared_Setup_Tables_Including_Owned_Rows_Have_The_Prefix()
	{
		using var context = CreateContext();
		var academicTypes = context.Model.GetEntityTypes().Where(entity =>
			entity.ClrType.Namespace?.StartsWith("ShikshaMitra.Academic.", StringComparison.Ordinal) == true ||
			entity.ClrType.Namespace?.StartsWith("ShikshaMitra.TenantSettings", StringComparison.Ordinal) == true).ToList();
		academicTypes.Count.ShouldBeGreaterThanOrEqualTo(102);
		foreach (var entity in academicTypes)
		{
			entity.GetTableName()!.ShouldStartWith("academic_");
			entity.GetTableName()!.ShouldNotStartWith("academic_Academic");
		}
		context.Model.FindEntityType(typeof(ShikshaMitra.Students.Student))!.GetTableName().ShouldBe("AppStudents");
		context.Model.FindEntityType(typeof(ShikshaMitra.HR.Employees.Employee))!.GetTableName().ShouldBe("AppEmployees");
		// Run `dotnet ef migrations has-pending-model-changes` in a separate process.
		// ABP test startup enables Npgsql's process-global legacy timestamp behavior,
		// while the design-time factory deliberately uses modern timestamp mappings.
	}

	[Fact]
	public void Duplicate_Academic_Name_Normalization_Only_Renames_Existing_Storage_Objects()
	{
		var migration = new NormalizeAcademicTableNames { ActiveProvider = Provider };
		var up = migration.UpOperations;
		var down = migration.DownOperations;
		up.OfType<RenameTableOperation>().Count().ShouldBe(12);
		down.OfType<RenameTableOperation>().Count().ShouldBe(12);
		foreach (var operation in up.Concat(down))
		{
			(operation is RenameTableOperation or RenameIndexOperation or SqlOperation).ShouldBeTrue();
			operation.IsDestructiveChange.ShouldBeFalse();
		}
		foreach (var rename in up.OfType<RenameTableOperation>())
		{
			rename.Name.ShouldStartWith("academic_Academic");
			rename.NewName.ShouldStartWith("academic_");
			rename.NewName.ShouldNotStartWith("academic_Academic");
			down.OfType<RenameTableOperation>().ShouldContain(reverse =>
				reverse.Name == rename.NewName && reverse.NewName == rename.Name);
		}
	}

	[Fact]
	public void Migration_And_Rollback_Only_Rename_Existing_Storage_Objects()
	{
		var migration = new PrefixAcademicTables { ActiveProvider = Provider };
		var up = migration.UpOperations;
		var down = migration.DownOperations;
		up.OfType<RenameTableOperation>().Count().ShouldBe(102);
		down.OfType<RenameTableOperation>().Count().ShouldBe(102);
		foreach (var operation in up.Concat(down))
		{
			(operation is RenameTableOperation or RenameIndexOperation or SqlOperation).ShouldBeTrue();
			operation.IsDestructiveChange.ShouldBeFalse();
			if (operation is SqlOperation sql)
			{
				(sql.Sql.StartsWith("ALTER TABLE ", StringComparison.Ordinal) ||
				 sql.Sql.StartsWith("DO $academic_prefix$", StringComparison.Ordinal)).ShouldBeTrue();
				sql.Sql.ShouldContain(" RENAME CONSTRAINT ");
			}
		}
		foreach (var rename in up.OfType<RenameTableOperation>())
			down.OfType<RenameTableOperation>().ShouldContain(reverse => reverse.Name == rename.NewName && reverse.NewName == rename.Name);
	}

	[AcademicSchemaPostgresFact]
	public async Task PostgreSql_Rename_And_Rollback_Preserve_Data_Storage_And_All_Foreign_Keys()
	{
		var connectionString = RequireDisposableConnection();
		using var context = CreateContext(connectionString);
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		(await ReadValuesAsync(connection, "SELECT tablename FROM pg_tables WHERE schemaname = 'public'")).ShouldBeEmpty();

		var assembly = context.GetService<IMigrationsAssembly>();
		var migrationEntry = assembly.Migrations.Single(item => item.Key.EndsWith("_PrefixAcademicTables", StringComparison.Ordinal));
		var previousEntry = assembly.Migrations.Where(item => string.CompareOrdinal(item.Key, migrationEntry.Key) < 0)
			.OrderByDescending(item => item.Key).First();
		var previous = assembly.CreateMigration(previousEntry.Value, Provider);
		var previousModel = context.GetService<IModelRuntimeInitializer>().Initialize(previous.TargetModel, designTime: true);
		var create = context.GetService<IMigrationsModelDiffer>().GetDifferences(null, previousModel.GetRelationalModel());
		var sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
		foreach (var command in sqlGenerator.Generate(create, previousModel))
			await ExecuteAsync(connection, command.CommandText);

		await ExecuteAsync(connection, """
			INSERT INTO "AppAcademicYears" ("Id", "TenantId", "Name", "StartDate", "EndDate", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('11111111-1111-1111-1111-111111111111', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'Preserve tenant A', '2026-04-01Z', '2027-03-31Z', '{}', 'original-a', now()),
			       ('22222222-2222-2222-2222-222222222222', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', 'Preserve tenant B', '2026-04-01Z', '2027-03-31Z', '{}', 'original-b', now());
			INSERT INTO "AppTeacherSubjectAllocations" ("Id", "StandardId", "DivisionId", "SubjectId", "ExtraProperties", "ConcurrencyStamp", "CreationTime")
			VALUES ('33333333-3333-3333-3333-333333333333', '44444444-4444-4444-4444-444444444444', '55555555-5555-5555-5555-555555555555', '66666666-6666-6666-6666-666666666666', '{}', 'original-allocation', now());
			INSERT INTO "AppAllocatedTeachers" ("TeacherSubjectAllocationId", "TeacherId", "CreationTime", "IsDeleted")
			VALUES ('33333333-3333-3333-3333-333333333333', '77777777-7777-7777-7777-777777777777', now(), false);
			""");

		var storageBefore = await ReadStorageAsync(connection);
		var constraintsBefore = await ReadConstraintsAsync(connection);
		var migration = assembly.CreateMigration(migrationEntry.Value, Provider);
		var currentModel = context.GetService<IDesignTimeModel>().Model;
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(migration.UpOperations, currentModel));
		(await ReadStorageAsync(connection)).ShouldBe(storageBefore);
		(await ReadConstraintsAsync(connection)).ShouldBe(constraintsBefore);
		(await ReadValuesAsync(connection, "SELECT \"Name\" || ':' || \"ConcurrencyStamp\" FROM \"academic_AcademicYears\" ORDER BY \"Name\""))
			.ShouldBe(new[] { "Preserve tenant A:original-a", "Preserve tenant B:original-b" });
		(await ReadValuesAsync(connection, "SELECT \"TeacherId\"::text FROM \"academic_AllocatedTeachers\""))
			.ShouldBe(new[] { "77777777-7777-7777-7777-777777777777" });
		(await ReadValuesAsync(connection, "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND starts_with(tablename, 'academic_')")).Count.ShouldBe(102);

		// The newly named owned relationship must still enforce its parent foreign key.
		var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection, """
			INSERT INTO "academic_AllocatedTeachers" ("TeacherSubjectAllocationId", "TeacherId", "CreationTime", "IsDeleted")
			VALUES ('99999999-9999-9999-9999-999999999999', '88888888-8888-8888-8888-888888888888', now(), false);
			"""));
		exception.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);

		// The performance index is a separate reversible change, never part of the storage rename.
		var indexMigration = new IndexAcademicLessonPlanPaging { ActiveProvider = Provider };
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(indexMigration.UpOperations, currentModel));
		var indexDefinition = (await ReadValuesAsync(connection, """
			SELECT indexdef FROM pg_indexes
			WHERE indexname = 'IX_academic_LessonPlans_TenantId_AcademicYearId_CreationTime'
			""")).Single();
		indexDefinition.ShouldContain("\"TenantId\", \"AcademicYearId\", \"CreationTime\" DESC");
		indexDefinition.ShouldContain("WHERE (NOT \"IsDeleted\")");
		(await ReadConstraintsAsync(connection)).ShouldBe(constraintsBefore);
		var storageWithIndex = await ReadStorageAsync(connection);

		var normalizationMigration = new NormalizeAcademicTableNames { ActiveProvider = Provider };
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(normalizationMigration.UpOperations, currentModel));
		(await ReadStorageAsync(connection)).ShouldBe(storageWithIndex);
		(await ReadConstraintsAsync(connection)).ShouldBe(constraintsBefore);
		(await ReadValuesAsync(connection, "SELECT \"Name\" || ':' || \"ConcurrencyStamp\" FROM \"academic_Years\" ORDER BY \"Name\""))
			.ShouldBe(new[] { "Preserve tenant A:original-a", "Preserve tenant B:original-b" });
		(await ReadValuesAsync(connection, "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND starts_with(tablename, 'academic_Academic')"))
			.ShouldBeEmpty();
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(normalizationMigration.DownOperations, currentModel));
		(await ReadStorageAsync(connection)).ShouldBe(storageWithIndex);
		(await ReadConstraintsAsync(connection)).ShouldBe(constraintsBefore);
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(indexMigration.DownOperations, currentModel));
		(await ReadStorageAsync(connection)).ShouldBe(storageBefore);

		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(migration.DownOperations, previousModel));
		(await ReadStorageAsync(connection)).ShouldBe(storageBefore);
		(await ReadConstraintsAsync(connection)).ShouldBe(constraintsBefore);
		(await ReadValuesAsync(connection, "SELECT \"TeacherId\"::text FROM \"AppAllocatedTeachers\""))
			.ShouldBe(new[] { "77777777-7777-7777-7777-777777777777" });
		(await ReadValuesAsync(connection, "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND starts_with(tablename, 'academic_')")).ShouldBeEmpty();

		// Match the older hand-written admission migration, which never created this FK.
		// Renaming tables must work for both historical databases and model-created databases.
		await ExecuteAsync(connection, "ALTER TABLE \"AppAdmissionOfferings\" DROP CONSTRAINT \"FK_AppAdmissionOfferings_AppEducationBoards_BoardId\"");
		var historicalConstraints = await ReadConstraintsAsync(connection);
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(migration.UpOperations, currentModel));
		(await ReadConstraintsAsync(connection)).ShouldBe(historicalConstraints);
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(normalizationMigration.UpOperations, currentModel));
		(await ReadConstraintsAsync(connection)).ShouldBe(historicalConstraints);
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(normalizationMigration.DownOperations, currentModel));
		(await ReadConstraintsAsync(connection)).ShouldBe(historicalConstraints);
		await ExecuteTransactionAsync(connection, sqlGenerator.Generate(migration.DownOperations, previousModel));
		(await ReadConstraintsAsync(connection)).ShouldBe(historicalConstraints);
		(await ReadStorageAsync(connection)).ShouldBe(storageBefore);
	}

	private static ShikshaMitraDbContext CreateContext(string? connectionString = null)
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		return new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connectionString ?? "Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only").Options);
	}

	private static string RequireDisposableConnection()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ACADEMIC_SCHEMA_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("academic_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Use an empty disposable loopback academic_tests_* PostgreSQL database.");
		return connection.ConnectionString;
	}

	private static Task<List<string>> ReadStorageAsync(NpgsqlConnection connection) => ReadValuesAsync(connection, """
		SELECT c.oid::text || ':' || c.relfilenode::text || ':' || c.relkind::text
		FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
		WHERE n.nspname = 'public' AND c.relkind IN ('r', 'i', 'S') ORDER BY c.oid
		""");

	private static Task<List<string>> ReadConstraintsAsync(NpgsqlConnection connection) => ReadValuesAsync(connection, """
		SELECT c.oid::text || ':' || c.conrelid::text || ':' || c.confrelid::text || ':' || c.contype::text || ':' || c.convalidated::text
		FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
		WHERE n.nspname = 'public' ORDER BY c.oid
		""");

	private static async Task<List<string>> ReadValuesAsync(NpgsqlConnection connection, string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync();
		var values = new List<string>();
		while (await reader.ReadAsync()) values.Add(reader.GetString(0));
		return values;
	}

	private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
	{
		await using var command = new NpgsqlCommand(sql, connection);
		await command.ExecuteNonQueryAsync();
	}

	private static async Task ExecuteTransactionAsync(NpgsqlConnection connection, IReadOnlyList<MigrationCommand> commands)
	{
		await using var transaction = await connection.BeginTransactionAsync();
		foreach (var command in commands) await ExecuteAsync(connection, command.CommandText);
		await transaction.CommitAsync();
	}
}

public sealed class AcademicSchemaPostgresFactAttribute : FactAttribute
{
	public AcademicSchemaPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ACADEMIC_SCHEMA_TEST_POSTGRES")))
			Skip = "Set ACADEMIC_SCHEMA_TEST_POSTGRES to an empty, disposable loopback academic_tests_* PostgreSQL database.";
	}
}
