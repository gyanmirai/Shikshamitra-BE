using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPublicInquirySubmissionLockPostgres_Tests
{
	[AssessmentRosterPostgresFact]
	public async Task Public_submission_locks_require_transaction_serialize_link_and_isolate_tenants_links_and_namespaces()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ASSESSMENT_ROSTER_TEST_POSTGRES"));
		connection.Database += "_public";
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
			static EfCoreAdmissionPublicInquirySubmissionLock Lock(ShikshaMitraDbContext db)
			{
				var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
				provider.GetDbContextAsync().Returns(db);
				return new(provider);
			}
			await Should.ThrowAsync<InvalidOperationException>(() => Lock(first).AcquireAsync(Guid.NewGuid(), Guid.NewGuid()));
			var tenant = Guid.NewGuid(); var link = Guid.NewGuid();
			await using var firstTransaction = await first.Database.BeginTransactionAsync();
			await using var secondTransaction = await second.Database.BeginTransactionAsync();
			await using var isolatedTransaction = await otherTenant.Database.BeginTransactionAsync();
			await Lock(first).AcquireAsync(tenant, link);
			await Lock(otherTenant).AcquireAsync(Guid.NewGuid(), link).WaitAsync(TimeSpan.FromSeconds(5));
			await Lock(otherTenant).AcquireAsync(tenant, Guid.NewGuid()).WaitAsync(TimeSpan.FromSeconds(5));
			var publicationProvider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
			publicationProvider.GetDbContextAsync().Returns(otherTenant);
			await new EfCoreAdmissionWorkflowPublicationLock(publicationProvider).AcquireAsync(tenant, link.ToString("N")).WaitAsync(TimeSpan.FromSeconds(5));
			var waiting = Lock(second).AcquireAsync(tenant, link);
			(await Task.WhenAny(waiting, Task.Delay(150))).ShouldNotBe(waiting);
			await firstTransaction.CommitAsync();
			await waiting.WaitAsync(TimeSpan.FromSeconds(5));
			await secondTransaction.CommitAsync(); await isolatedTransaction.CommitAsync();
			await VerifyBoundedReplayLookupAsync(options, first);
		}
		finally
		{
			NpgsqlConnection.ClearAllPools();
			await using var drop = new NpgsqlCommand($"DROP DATABASE \"{connection.Database}\" WITH (FORCE)", admin);
			await drop.ExecuteNonQueryAsync();
		}
	}

	private static async Task VerifyBoundedReplayLookupAsync(DbContextOptions<ShikshaMitraDbContext> options, ShikshaMitraDbContext setup)
	{
		await setup.Database.ExecuteSqlRawAsync("""
			CREATE TABLE "AppAdmissionInquiries" ("Id" uuid PRIMARY KEY, "TenantId" uuid NULL,
			"AdmissionCycleId" uuid NOT NULL, "IsDeleted" boolean NOT NULL, "ExtraProperties" jsonb NOT NULL)
			""");
		var tenant = Guid.NewGuid(); var cycle = Guid.NewGuid(); var link = Guid.NewGuid(); var key = Guid.NewGuid();
		static string Metadata(Guid linkId, Guid requestId, string hash) => JsonSerializer.Serialize(new Dictionary<string, string>
		{
			["AdmissionPublicSubmissionLinkId"] = linkId.ToString("N"),
			["AdmissionPublicSubmissionRequestId"] = requestId.ToString("N"),
			["AdmissionPublicSubmissionFingerprint"] = hash,
			["LargeUnrelatedAnswer"] = new string('x', 2048)
		});
		var unrelated = Metadata(Guid.NewGuid(), Guid.NewGuid(), "UNRELATED");
		await setup.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO "AppAdmissionInquiries" ("Id", "TenantId", "AdmissionCycleId", "IsDeleted", "ExtraProperties")
			SELECT md5(number::text)::uuid, {tenant}, {cycle}, false, CAST({unrelated} AS jsonb) FROM generate_series(1, 1000) AS number
			""");
		async Task Add(Guid? rowTenant, Guid rowCycle, Guid rowLink, Guid rowKey, string hash, bool deleted = false)
		{
			var id = Guid.NewGuid(); var json = Metadata(rowLink, rowKey, hash);
			await setup.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO "AppAdmissionInquiries" ("Id", "TenantId", "AdmissionCycleId", "IsDeleted", "ExtraProperties")
				VALUES ({id}, {rowTenant}, {rowCycle}, {deleted}, CAST({json} AS jsonb))
				""");
		}
		await Add(Guid.NewGuid(), cycle, link, key, "FOREIGN_TENANT");
		await Add(tenant, Guid.NewGuid(), link, key, "FOREIGN_CYCLE");
		await Add(tenant, cycle, Guid.NewGuid(), key, "FOREIGN_LINK");
		await Add(null, cycle, link, key, "HOST");
		await Add(tenant, cycle, link, key, "EXPECTED");
		var deletedKey = Guid.NewGuid(); await Add(tenant, cycle, link, deletedKey, "DELETED", true);
		var commands = new ReplayCommandRecorder();
		await using var lookupDb = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>(options).AddInterceptors(commands).Options);
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(lookupDb);
		// Reflection keeps independent Task1 test compilation compatible while this new EF owner is built separately.
		var type = typeof(EfCoreAdmissionPublicInquirySubmissionLock).Assembly.GetType("ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions.EfCoreAdmissionPublicInquirySubmissionRepository");
		type.ShouldNotBeNull(); var repository = Activator.CreateInstance(type, provider)!;
		async Task<string?> Find(Guid? lookupTenant, Guid lookupLink, Guid? requestId, string hash)
		{
			var count = commands.Reads.Count;
			var result = await (Task<string?>)type.GetMethod("FindFingerprintAsync")!.Invoke(repository, [lookupTenant, cycle, lookupLink, requestId, hash])!;
			commands.Reads.Count.ShouldBe(count + 1, "A replay lookup must issue one bounded scalar read regardless of unrelated inquiry count.");
			return result;
		}
		(await Find(tenant, link, key, "CHANGED")).ShouldBe("EXPECTED");
		(await Find(tenant, link, null, "EXPECTED")).ShouldBe("EXPECTED");
		(await Find(tenant, link, null, "CHANGED")).ShouldBeNull();
		(await Find(tenant, link, Guid.NewGuid(), "EXPECTED")).ShouldBeNull();
		(await Find(tenant, Guid.NewGuid(), key, "EXPECTED")).ShouldBeNull();
		(await Find(Guid.NewGuid(), link, key, "EXPECTED")).ShouldBeNull();
		(await Find(null, link, key, "EXPECTED")).ShouldBe("HOST");
		(await Find(tenant, link, deletedKey, "DELETED")).ShouldBeNull();
		commands.Reads.ShouldAllBe(command => command.Contains("LIMIT 1", StringComparison.Ordinal) && !command.Contains("SELECT *", StringComparison.Ordinal));
	}

	private sealed class ReplayCommandRecorder : DbCommandInterceptor
	{
		public List<string> Reads { get; } = [];
		public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
			InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
		{
			Reads.Add(command.CommandText);
			return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
		}
	}
}

