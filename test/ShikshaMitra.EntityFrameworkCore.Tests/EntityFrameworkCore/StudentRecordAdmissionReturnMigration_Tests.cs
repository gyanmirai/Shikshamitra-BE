using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore;

public class StudentRecordAdmissionReturnMigration_Tests
{
	private const string Previous = "20261001043143_AddUniversalStudentAttendance";
	private const string Current = "20261001060643_AddStudentRecordAdmissionReturn";
	private const string Handoffs = "AppStudentRecordReturnHandoffs";
	private const string Transitions = "AppStudentRecordTransitions";

	[Fact]
	public void Reviewed_return_target_model_matches_records_runtime_and_changes_only_return_tables()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=localhost;Database=records_tests_model;Username=unused;Password=unused").Options);
		var assembly = db.GetService<IMigrationsAssembly>();
		var initializer = db.GetService<IModelRuntimeInitializer>();
		var previous = initializer.Initialize(assembly.CreateMigration(assembly.Migrations[Previous], "Npgsql.EntityFrameworkCore.PostgreSQL").TargetModel, designTime: true);
		var current = initializer.Initialize(assembly.CreateMigration(assembly.Migrations[Current], "Npgsql.EntityFrameworkCore.PostgreSQL").TargetModel, designTime: true);
		var differ = db.GetService<IMigrationsModelDiffer>();
		var changes = differ.GetDifferences(previous.GetRelationalModel(), current.GetRelationalModel());
		changes.Count.ShouldBeGreaterThan(0);
		changes.ShouldAllBe(x => Table(x) == Handoffs || Table(x) == Transitions);
		var runtime = db.GetService<IDesignTimeModel>().Model;
		differ.GetDifferences(current.GetRelationalModel(), runtime.GetRelationalModel())
			.Where(x => Table(x) == Handoffs || Table(x) == Transitions).ShouldBeEmpty();
		runtime.FindEntityType(typeof(StudentRecordTransition))!.FindProperty("SourceEnrollmentId")!.IsNullable.ShouldBeTrue();
		runtime.FindEntityType(typeof(StudentRecordReturnHandoff))!.GetIndexes()
			.Single(x => x.Properties.Any(p => p.Name == "AdmissionApplicationId")).GetFilter()
			.ShouldBe("\"TransitionId\" IS NOT NULL AND \"AdmissionApplicationId\" IS NOT NULL");
	}

	[StudentRecordsPostgresFact]
	public async Task Reviewed_return_migration_preserves_legacy_duplicates_enforces_new_links_and_refuses_history_loss()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDENT_RECORDS_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database?.StartsWith("records_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Only a fresh empty loopback Records test database is allowed.");
		await using var sql = new NpgsqlConnection(connection.ConnectionString); await sql.OpenAsync();
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')")).ShouldBe(0);
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
		var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(Previous);
		var tenant = Guid.NewGuid(); var application = Guid.NewGuid();
		await InsertAsync(sql, tenant, application, "legacy-one"); await InsertAsync(sql, tenant, application, "legacy-two");
		const string snapshot = "SELECT md5(string_agg((to_jsonb(h) - ARRAY['AdmissionInquiryId','AdmissionOfferingId','EntryPath','TransitionId','SourceSnapshotJson'])::text, '|' ORDER BY \"Id\")) FROM \"AppStudentRecordReturnHandoffs\" h WHERE \"IdempotencyKey\" LIKE 'legacy-%'";
		var before = await ScalarAsync<string>(sql, snapshot);
		await migrator.MigrateAsync(Current); (await ScalarAsync<string>(sql, snapshot)).ShouldBe(before);
		await migrator.MigrateAsync(Current); (await ScalarAsync<string>(sql, snapshot)).ShouldBe(before);
		(await ScalarAsync<string>(sql, "SELECT is_nullable FROM information_schema.columns WHERE table_name='AppStudentRecordTransitions' AND column_name='SourceEnrollmentId'")).ShouldBe("YES");
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordReturnHandoffs\" WHERE \"TransitionId\" IS NULL AND \"SourceSnapshotJson\" = '{}'" )).ShouldBe(2);
		var inquiry = Guid.NewGuid();
		await InsertAsync(sql, tenant, null, "governed-inquiry", Guid.NewGuid(), inquiry);
		var duplicateInquiry = await Should.ThrowAsync<PostgresException>(() => InsertAsync(sql, tenant, null, "duplicate-inquiry", Guid.NewGuid(), inquiry));
		duplicateInquiry.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		var transition = Guid.NewGuid();
		await InsertAsync(sql, tenant, application, "governed-direct", transition);
		(await Should.ThrowAsync<PostgresException>(() => InsertAsync(sql, tenant, application, "duplicate-application", Guid.NewGuid())))
			.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		(await Should.ThrowAsync<PostgresException>(() => InsertAsync(sql, tenant, Guid.NewGuid(), "duplicate-transition", transition)))
			.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
		var rejected = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(Previous));
		rejected.SqlState.ShouldBe(PostgresErrorCodes.RaiseException); rejected.MessageText.ShouldContain("forward fix");
		(await ScalarAsync<long>(sql, "SELECT count(*) FROM \"AppStudentRecordReturnHandoffs\"")).ShouldBe(4);
		(await ScalarAsync<string>(sql, snapshot)).ShouldBe(before);
		await ExecuteAsync(sql, "DELETE FROM \"AppStudentRecordReturnHandoffs\" WHERE \"TransitionId\" IS NOT NULL");
		await migrator.MigrateAsync(Previous); (await ScalarAsync<string>(sql, snapshot)).ShouldBe(before);
		await migrator.MigrateAsync(Current); (await ScalarAsync<string>(sql, snapshot)).ShouldBe(before);
	}

	private static async Task InsertAsync(NpgsqlConnection sql, Guid tenant, Guid? application, string key, Guid? transition = null, Guid? inquiry = null)
	{
		var governed = transition.HasValue;
		var columns = governed ? ", \"TransitionId\", \"AdmissionInquiryId\", \"SourceSnapshotJson\"" : "";
		var values = governed ? ", @transition, @inquiry, '{}'" : "";
		await using var command = new NpgsqlCommand($"INSERT INTO \"AppStudentRecordReturnHandoffs\" (\"Id\", \"TenantId\", \"StudentId\", \"AcademicYearId\", \"AdmissionApplicationId\", \"Route\", \"IdempotencyKey\", \"RequestJson\", \"PolicyJson\", \"ExtraProperties\", \"ConcurrencyStamp\", \"CreationTime\"{columns}) VALUES (@id,@tenant,@student,@year,@application,'FreshApplication',@key,'{{}}','{{}}','{{}}','synthetic-return','2026-10-01Z'{values})", sql);
		command.Parameters.AddWithValue("id", Guid.NewGuid()); command.Parameters.AddWithValue("tenant", tenant);
		command.Parameters.AddWithValue("student", Guid.NewGuid()); command.Parameters.AddWithValue("year", Guid.NewGuid());
		command.Parameters.AddWithValue("application", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)application ?? DBNull.Value); command.Parameters.AddWithValue("key", key);
		if (governed) { command.Parameters.AddWithValue("transition", transition!.Value); command.Parameters.AddWithValue("inquiry", NpgsqlTypes.NpgsqlDbType.Uuid, (object?)inquiry ?? DBNull.Value); }
		await command.ExecuteNonQueryAsync();
	}
	private static string? Table(MigrationOperation operation) => operation.GetType().GetProperty("Table")?.GetValue(operation) as string;
	private static async Task<T> ScalarAsync<T>(NpgsqlConnection sql, string query) { await using var command = new NpgsqlCommand(query, sql); return (T)(await command.ExecuteScalarAsync())!; }
	private static async Task ExecuteAsync(NpgsqlConnection sql, string query) { await using var command = new NpgsqlCommand(query, sql); await command.ExecuteNonQueryAsync(); }
}
