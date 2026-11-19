using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.EntityFrameworkCore;

public sealed class StudentRecordsMigrationCopyFactAttribute : FactAttribute
{
	public StudentRecordsMigrationCopyFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(StudentRecordsMigrationCopy_Tests.ConnectionVariable)))
			Skip = "Requires a separately restored pre-Records loopback records_tests_copy_* database.";
	}
}

/// <summary>Rehearse the three Records migrations against a restored populated school database.</summary>
public sealed class StudentRecordsMigrationCopy_Tests(ITestOutputHelper output)
{
	public const string ConnectionVariable = "STUDENT_RECORDS_MIGRATION_COPY_POSTGRES";
	private const string PreviousMigration = "20260928183735_AddStudentPortalAccountLinks";
	private static readonly string[] RecordsMigrations = ["20260930175531_AddStudentRecordGovernanceAndOperations",
		"20260930182702_AddStudentRecordTransitionTargetScope", "20261001021100_AddStudentRecordRenewalHistory"];

	[StudentRecordsMigrationCopyFact]
	public async Task Record_migrations_preserve_every_existing_table_row_and_index_in_populated_school_copy()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^records_tests_copy_[a-f0-9]{32}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Only a separately restored disposable loopback school copy is allowed.");
		await using var sql = new NpgsqlConnection(connection.ConnectionString); await sql.OpenAsync();
		var originalHistory = await StringsAsync(sql, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"");
		originalHistory.Last().ShouldBe(PreviousMigration, "Refuse an already migrated or unrelated schema.");
		var tables = await StringsAsync(sql, "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory' ORDER BY tablename");
		var columns = new Dictionary<string, List<string>>(); var snapshots = new Dictionary<string, (long Count, string Digest)>();
		foreach (var table in tables)
		{
			await using var columnQuery = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table ORDER BY ordinal_position", sql);
			columnQuery.Parameters.AddWithValue("table", table); columns[table] = [];
			await using (var reader = await columnQuery.ExecuteReaderAsync())
				while (await reader.ReadAsync()) columns[table].Add(reader.GetString(0));
			snapshots[table] = await SnapshotAsync(sql, table, columns[table]);
		}
		snapshots["AppStudents"].Count.ShouldBeGreaterThan(0, "The release rehearsal must use populated Student data.");
		var originalIndexes = await StringsAsync(sql, "SELECT indexname || ':' || indexdef FROM pg_indexes WHERE schemaname = 'public' ORDER BY indexname");
		var originalConstraints = await StringsAsync(sql, "SELECT conrelid::regclass::text || ':' || conname || ':' || pg_get_constraintdef(oid) FROM pg_constraint WHERE connamespace = 'public'::regnamespace ORDER BY conrelid::regclass::text, conname");

		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
		await database.GetService<IMigrator>().MigrateAsync(RecordsMigrations[^1]);
		(await StringsAsync(sql, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\""))
			.ShouldBe(originalHistory.Concat(RecordsMigrations).ToList(), "Apply exactly the three reviewed Records migrations.");
		foreach (var table in tables)
			(await SnapshotAsync(sql, table, columns[table])).ShouldBe(snapshots[table], $"Existing data changed in {table}.");
		var currentIndexes = await StringsAsync(sql, "SELECT indexname || ':' || indexdef FROM pg_indexes WHERE schemaname = 'public' ORDER BY indexname");
		foreach (var index in originalIndexes) currentIndexes.ShouldContain(index);
		var currentConstraints = await StringsAsync(sql, "SELECT conrelid::regclass::text || ':' || conname || ':' || pg_get_constraintdef(oid) FROM pg_constraint WHERE connamespace = 'public'::regnamespace ORDER BY conrelid::regclass::text, conname");
		foreach (var constraint in originalConstraints) currentConstraints.ShouldContain(constraint);
		var renewalIndex = await StringsAsync(sql, "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'AppStudentRecordRenewalCases' AND indexdef LIKE '%UNIQUE%' AND indexdef LIKE '%AcademicYearId%'");
		renewalIndex.Single().ShouldContain("Completed"); renewalIndex.Single().ShouldContain("Cancelled");
		// A deployment retry must not append history or touch any pre-existing data.
		await database.GetService<IMigrator>().MigrateAsync(RecordsMigrations[^1]);
		(await StringsAsync(sql, "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\""))
			.ShouldBe(originalHistory.Concat(RecordsMigrations).ToList());
		foreach (var table in tables) (await SnapshotAsync(sql, table, columns[table])).ShouldBe(snapshots[table]);
		output.WriteLine($"Preserved all original columns of {tables.Count} tables ({snapshots.Values.Sum(x => x.Count)} rows), including {snapshots["AppStudents"].Count} Student rows; existing indexes and constraints remain intact. Reapply is idempotent.");
		output.WriteLine($"Enrollment rows in this copy: {snapshots["AppStudentEnrollments"].Count}; no populated enrollment-history claim is made when this count is zero.");
	}

	private static async Task<List<string>> StringsAsync(NpgsqlConnection connection, string query)
	{
		await using var command = new NpgsqlCommand(query, connection); await using var reader = await command.ExecuteReaderAsync();
		var result = new List<string>(); while (await reader.ReadAsync()) result.Add(reader.GetString(0)); return result;
	}
	private static async Task<(long Count, string Digest)> SnapshotAsync(NpgsqlConnection connection, string table, List<string> columns)
	{
		static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
		// Hash only original columns so additive nullable certificate metadata does not masquerade as a data rewrite.
		var query = $"SELECT count(*), md5(COALESCE(string_agg(row_data, '|' ORDER BY row_data), '')) FROM (SELECT to_jsonb(original)::text AS row_data FROM (SELECT {string.Join(",", columns.Select(Quote))} FROM public.{Quote(table)}) original) rows";
		await using var command = new NpgsqlCommand(query, connection); await using var reader = await command.ExecuteReaderAsync();
		(await reader.ReadAsync()).ShouldBeTrue(); return (reader.GetInt64(0), reader.GetString(1));
	}
}
