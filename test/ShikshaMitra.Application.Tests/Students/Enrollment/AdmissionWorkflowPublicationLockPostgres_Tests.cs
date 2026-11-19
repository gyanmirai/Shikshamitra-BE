using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowPublicationLockPostgres_Tests
{
	[AssessmentRosterPostgresFact]
	public async Task Workflow_publication_locks_serialize_same_family_but_not_other_tenants_or_codes()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES"));
		connection.Database += "_workflow_locks";
		if (connection.Host is not ("localhost" or "127.0.0.1" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^admissions_assessment_tests_[a-z0-9_]{1,32}$"))
			throw new InvalidOperationException("Only a new disposable loopback Assessment test database is allowed.");
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
		await admin.OpenAsync();
		await using (var exists = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname=@name", admin))
		{
			exists.Parameters.AddWithValue("name", connection.Database);
			Convert.ToInt64(await exists.ExecuteScalarAsync()).ShouldBe(0, "Never reuse or remove an existing database.");
		}
		await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", admin)) await create.ExecuteNonQueryAsync();
		try
		{
			// Scheduling locks execute raw provider SQL and need no school/identity tables or seeding.
			var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString)
				.UseModel(new ModelBuilder().FinalizeModel()).Options;
			await using var first = new ShikshaMitraDbContext(options);
			await using var second = new ShikshaMitraDbContext(options);
			await using var otherTenant = new ShikshaMitraDbContext(options);
			static EfCoreAdmissionWorkflowPublicationLock Lock(ShikshaMitraDbContext db)
			{
				var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
				provider.GetDbContextAsync().Returns(db);
				return new(provider);
			}
			await Should.ThrowAsync<InvalidOperationException>(() => Lock(first).AcquireAsync(Guid.NewGuid(), "STANDARD"));
			var tenant = Guid.NewGuid(); const string code = "STANDARD";
			await using var firstTransaction = await first.Database.BeginTransactionAsync();
			await using var secondTransaction = await second.Database.BeginTransactionAsync();
			await using var isolatedTransaction = await otherTenant.Database.BeginTransactionAsync();
			await Lock(first).AcquireAsync(tenant, code);
			await Lock(otherTenant).AcquireAsync(Guid.NewGuid(), code).WaitAsync(TimeSpan.FromSeconds(5));
			await Lock(otherTenant).AcquireAsync(tenant, "DIFFERENT").WaitAsync(TimeSpan.FromSeconds(5));
			var waiting = Lock(second).AcquireAsync(tenant, code);
			(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
			await firstTransaction.CommitAsync();
			await waiting.WaitAsync(TimeSpan.FromSeconds(5));
			await secondTransaction.CommitAsync(); await isolatedTransaction.CommitAsync();
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
			await drop.ExecuteNonQueryAsync();
		}
	}
}
