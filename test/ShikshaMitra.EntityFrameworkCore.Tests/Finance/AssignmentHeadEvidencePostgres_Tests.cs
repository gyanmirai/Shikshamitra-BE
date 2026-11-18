using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Migrations;
using Shouldly;
using Xunit;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.EntityFrameworkCore;

public sealed class AssignmentEvidencePostgresFactAttribute : FactAttribute
{
	public AssignmentEvidencePostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FINANCE_EVIDENCE_TEST_POSTGRES")))
			Skip = "Requires an empty disposable loopback payroll_tests_* database in FINANCE_EVIDENCE_TEST_POSTGRES.";
	}
}

public class AssignmentHeadEvidencePostgres_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Production_dependency_query_translates_to_two_bounded_provider_aggregates_without_connecting(bool sqlite)
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>();
		if (sqlite) options.UseSqlite("Data Source=:memory:");
		else options.UseNpgsql("Host=127.0.0.1;Port=1;Database=payroll_tests_offline_query");
		await using var db = new ShikshaMitraDbContext(options.Options);
		var executor = DispatchProxy.Create<IAsyncQueryableExecuter, SqlCaptureExecutor>();
		// This standalone context has no ABP service provider. Real tenant/delete filter
		// execution is covered by the SQLite application fixtures; this test compiles SQL.
		var detailRepository = QueryRepository(db.Set<FeeStructureDetail>().IgnoreQueryFilters(), executor);
		var dueRepository = QueryRepository(db.Set<DueFee>().IgnoreQueryFilters(), executor);
		var assignmentRepository = QueryRepository(db.Set<StudentFeeStructure>().IgnoreQueryFilters(), executor);
		var headRepository = QueryRepository(db.Set<ShikshaMitra.Domain.Finance.Fees.Settings.FeeHead>().IgnoreQueryFilters(), executor);
		var production = new FeeHeadDependencyQuery(detailRepository, dueRepository, assignmentRepository, headRepository);
		await production.GetAsync([Guid.NewGuid()]);
		var commands = ((SqlCaptureExecutor)(object)executor).Commands;
		commands.Count.ShouldBe(2);
		commands.ShouldAllBe(sql => sql.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
		commands.ShouldNotContain(sql => sql.Contains("AssignmentSnapshotJson", StringComparison.Ordinal));
		commands.ShouldContain(sql => sql.Contains("UNION", StringComparison.OrdinalIgnoreCase) && sql.Contains("IncludedFeeHeadIds", StringComparison.Ordinal));
		commands.ShouldNotContain(sql => sql.Contains("APPLY", StringComparison.OrdinalIgnoreCase));
	}

	private static IRepository<T, Guid> QueryRepository<T>(IQueryable<T> query, IAsyncQueryableExecuter executor)
		where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var repository = Substitute.For<IRepository<T, Guid>>();
		repository.GetQueryableAsync().Returns(Task.FromResult(query));
		repository.AsyncExecuter.Returns(executor);
		return repository;
	}

	// Compile each real production IQueryable with Npgsql, but never open a connection.
	// Generic anonymous projection results are empty because only translation is under test.
	public class SqlCaptureExecutor : DispatchProxy
	{
		public List<string> Commands { get; } = [];
		protected override object? Invoke(MethodInfo? method, object?[]? arguments)
		{
			if (method?.Name != "ToListAsync" || arguments?[0] is not IQueryable query)
				throw new NotSupportedException(method?.Name);
			Commands.Add(query.ToQueryString());
			var resultType = method.ReturnType.GetGenericArguments()[0];
			return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType)
				.Invoke(null, [Activator.CreateInstance(resultType)]);
		}
	}

	[Fact]
	public void Rollback_locks_before_checking_captured_placement()
	{
		var migration = new IndexAssignmentHeadEvidence();
		var guard = migration.DownOperations.First().ShouldBeOfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>();
		guard.Sql.ShouldContain("IN ACCESS EXCLUSIVE MODE");
		guard.Sql.IndexOf("LOCK TABLE", StringComparison.Ordinal).ShouldBeLessThan(guard.Sql.IndexOf("IF EXISTS", StringComparison.Ordinal));
		guard.Sql.ShouldContain("AcademicStandardId");
		guard.Sql.ShouldContain("RAISE EXCEPTION");
		guard.SuppressTransaction.ShouldBeFalse();
	}

	[AssignmentEvidencePostgresFact]
	public async Task Backfill_preserves_zero_selections_corrupt_fallback_and_immutable_source_evidence()
	{
		var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FINANCE_EVIDENCE_TEST_POSTGRES"));
		if (settings.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			settings.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Evidence migration tests require an empty disposable loopback payroll_tests_* database.");
		await using var connection = new NpgsqlConnection(settings.ConnectionString);
		await connection.OpenAsync();
		await using (var check = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')", connection))
			((long)(await check.ExecuteScalarAsync())!).ShouldBe(0, "Never reuse or reset an existing database.");
		await using var transaction = await connection.BeginTransactionAsync();
		await using (var create = new NpgsqlCommand("""
			CREATE TEMP TABLE "Finance_StudentFeeStructures"
			("Id" integer PRIMARY KEY, "AssignmentSnapshotJson" text, "OriginalAmount" numeric NOT NULL) ON COMMIT DROP;
			""", connection, transaction))
			await create.ExecuteNonQueryAsync();
		var selected = Guid.NewGuid();
		string?[] snapshots = [
			JsonSerializer.Serialize(new { Heads = new[] { new { FeeHeadId = selected, Included = true, Amount = 0 }, new { FeeHeadId = selected, Included = true, Amount = 0 }, new { FeeHeadId = Guid.NewGuid(), Included = false, Amount = 10 } } }),
			"{\"Heads\":[]}", null, "{invalid", "{\"Heads\":null}", "{\"Heads\":[{\"Included\":true,\"FeeHeadId\":\"invalid\"}]}"
		];
		for (var index = 0; index < snapshots.Length; index++)
		{
			await using var insert = new NpgsqlCommand("INSERT INTO \"Finance_StudentFeeStructures\" VALUES (@id,@snapshot,123.45)", connection, transaction);
			insert.Parameters.AddWithValue("id", index);
			insert.Parameters.AddWithValue("snapshot", NpgsqlTypes.NpgsqlDbType.Text, (object?)snapshots[index] ?? DBNull.Value);
			await insert.ExecuteNonQueryAsync();
		}
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(settings.ConnectionString).Options);
		var migration = new IndexAssignmentHeadEvidence();
		foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.Model))
		{
			await using var execute = new NpgsqlCommand(command.CommandText, connection, transaction);
			await execute.ExecuteNonQueryAsync();
		}
		await using (var read = new NpgsqlCommand("SELECT * FROM \"Finance_StudentFeeStructures\" ORDER BY \"Id\"", connection, transaction))
		await using (var rows = await read.ExecuteReaderAsync())
		{
			var index = 0;
			while (await rows.ReadAsync())
			{
				(rows.IsDBNull(1) ? null : rows.GetString(1)).ShouldBe(snapshots[index]);
				rows.GetDecimal(2).ShouldBe(123.45m);
				if (index == 0) rows.GetFieldValue<Guid[]>(3).ShouldBe([selected]);
				else if (index == 1) rows.GetFieldValue<Guid[]>(3).ShouldBeEmpty();
				else rows.IsDBNull(3).ShouldBeTrue();
				rows.IsDBNull(4).ShouldBeTrue(); rows.IsDBNull(5).ShouldBeTrue(); rows.IsDBNull(6).ShouldBeTrue();
				index++;
			}
			index.ShouldBe(snapshots.Length);
		}
		await using (var placement = new NpgsqlCommand("UPDATE \"Finance_StudentFeeStructures\" SET \"AcademicStandardId\" = @standard WHERE \"Id\" = 0", connection, transaction))
		{
			placement.Parameters.AddWithValue("standard", Guid.NewGuid());
			await placement.ExecuteNonQueryAsync();
		}
		var rollbackGuard = migration.DownOperations.First().ShouldBeOfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>();
		await using (var rollback = new NpgsqlCommand(rollbackGuard.Sql, connection, transaction))
		{
			var rejected = await Should.ThrowAsync<PostgresException>(() => rollback.ExecuteNonQueryAsync());
			rejected.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
			rejected.MessageText.ShouldContain("Academic assignment evidence exists");
		}
		// The transaction only touches a temporary table and rolls back all generated DDL.
		await transaction.RollbackAsync();
	}
}
