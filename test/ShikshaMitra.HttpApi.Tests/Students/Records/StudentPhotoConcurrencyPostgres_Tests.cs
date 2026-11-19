using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.Records;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.HttpApi.Tests.Students.Records;

public sealed class StudentPhotoPostgresFactAttribute : FactAttribute
{
	public StudentPhotoPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STUDENT_PHOTO_POSTGRES")))
			Skip = "Requires an explicitly configured disposable loopback photo_tests_* PostgreSQL database.";
	}
}

/// <summary>Exercises the actual EF photo fence against PostgreSQL, including admission's prior Serializable snapshot.</summary>
public class StudentPhotoConcurrencyPostgres_Tests
{
	[StudentPhotoPostgresFact]
	public async Task Portal_and_staff_read_committed_requests_observe_one_active_change() => await OverlapAsync(IsolationLevel.ReadCommitted);

	[StudentPhotoPostgresFact]
	public async Task Admission_joined_serializable_snapshot_cannot_insert_a_second_active_photo() => await OverlapAsync(IsolationLevel.Serializable);

	private static async Task OverlapAsync(IsolationLevel competingIsolation)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("STUDENT_PHOTO_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database?.StartsWith("photo_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Only disposable loopback photo_tests_* databases are allowed.");
		var schema = "photo_" + Guid.NewGuid().ToString("N");
		await using var setup = new NpgsqlConnection(connection.ConnectionString);
		await setup.OpenAsync();
		await new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", setup).ExecuteNonQueryAsync();
		connection.SearchPath = schema;
		await using var first = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
		await using var second = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
		var student = Guid.NewGuid();
		var tenant = Guid.NewGuid();
		await first.Database.ExecuteSqlRawAsync("CREATE TABLE \"AppStudents\" (\"Id\" uuid PRIMARY KEY, \"TenantId\" uuid, \"ConcurrencyStamp\" text, \"IsDeleted\" boolean NOT NULL); CREATE TABLE \"PhotoChanges\" (\"StudentId\" uuid, \"ClientKey\" text, \"Status\" text)");
		await first.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AppStudents\" VALUES ({student}, {tenant}, 'unchanged', false)");
		await using var portal = await first.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
		await using var competing = await second.Database.BeginTransactionAsync(competingIsolation);
		// Admission may have already read the student before it reaches photo review.
		await second.Database.ExecuteSqlRawAsync("SELECT * FROM \"AppStudents\"");
		await Fence(first).AcquireAsync(tenant, student);
		var waiting = Fence(second).AcquireAsync(tenant, student);
		await Task.Delay(200);
		waiting.IsCompleted.ShouldBeFalse();
		await first.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"PhotoChanges\" VALUES ({student}, 'portal', 'Pending')");
		await portal.CommitAsync();
		if (competingIsolation == IsolationLevel.Serializable)
		{
			var conflict = await Should.ThrowAsync<PostgresException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(10)));
			conflict.SqlState.ShouldBe(PostgresErrorCodes.SerializationFailure);
			await competing.RollbackAsync();
		}
		else
		{
			await waiting.WaitAsync(TimeSpan.FromSeconds(10));
			// This is the post-fence active-change query used by both creation paths.
			var count = await second.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"PhotoChanges\" WHERE \"Status\" = 'Pending'").SingleAsync();
			count.ShouldBe(1);
			await competing.CommitAsync();
		}
		(await first.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"PhotoChanges\"").SingleAsync()).ShouldBe(1);
		(await first.Database.SqlQueryRaw<string>("SELECT \"ConcurrencyStamp\" AS \"Value\" FROM \"AppStudents\"").SingleAsync()).ShouldBe("unchanged");
		await first.Database.CloseConnectionAsync();
		await second.Database.CloseConnectionAsync();
		await new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", setup).ExecuteNonQueryAsync();
	}

	private static EfCoreStudentPhotoChangeLock Fence(ShikshaMitraDbContext db)
	{
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		provider.GetDbContextAsync().Returns(db);
		return new(provider);
	}
}
