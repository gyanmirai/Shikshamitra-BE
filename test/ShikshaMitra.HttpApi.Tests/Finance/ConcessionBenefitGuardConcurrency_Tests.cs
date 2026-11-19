using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

public sealed class ConcessionBenefitPostgresFactAttribute : FactAttribute
{
	public ConcessionBenefitPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConcessionBenefitGuardDatabase.ConnectionVariable)))
			Skip = "Requires a fresh disposable loopback concession_benefit_test_* PostgreSQL database.";
	}
}

public sealed class ConcessionBenefitPostgresTheoryAttribute : TheoryAttribute
{
	public ConcessionBenefitPostgresTheoryAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConcessionBenefitGuardDatabase.ConnectionVariable)))
			Skip = "Requires a fresh disposable loopback concession_benefit_test_* PostgreSQL database.";
	}
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConcessionBenefitGuardCollection : ICollectionFixture<ConcessionBenefitGuardDatabase>
{
	public const string Name = "PostgreSQL concession benefit guard";
}

/// <summary>
/// Executes the production repository guard against real PostgreSQL transactions.
/// Only the projected header schema and ambient-UOW context plumbing are isolated;
/// advisory locks, SQL reads, snapshots and connection-pool behavior are not mocked.
/// Full request/approval behavior is covered separately by the HTTP workflow tests.
/// </summary>
[Collection(ConcessionBenefitGuardCollection.Name)]
[Trait("Module", "Finance")]
[Trait("Tier", "Critical")]
public sealed class ConcessionBenefitGuardConcurrency_Tests(ConcessionBenefitGuardDatabase database)
{
	[ConcessionBenefitPostgresTheory]
	[InlineData(IsolationLevel.ReadCommitted)]
	[InlineData(IsolationLevel.Serializable)]
	public async Task Concurrent_claim_sees_committed_approval_after_wait_without_changing_outer_isolation(IsolationLevel isolation)
	{
		var key = BenefitKey.New();
		await using var first = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, IsolationLevel.ReadCommitted);
		await using var second = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, isolation);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
		// Establish the second transaction's snapshot before the first writer commits.
		Assert.Null(await second.Repository.FindBlockingBenefitAsync(key.StudentId, key.TypeId, key.YearId));
		Assert.Null(await first.Repository.FindBlockingBenefitForMutationAsync(key.StudentId, key.TypeId, key.YearId));
		var waiting = second.Repository.FindBlockingBenefitForMutationAsync(key.StudentId, key.TypeId, key.YearId,
			cancellationToken: timeout.Token);
		try
		{
			await database.WaitForAdvisoryWaitAsync(second.ProcessId, waiting, timeout.Token);
			var approvedId = Guid.NewGuid();
			await database.InsertAsync(first.Outer, key, approvedId, ConcessionRequestStatus.Approved);
			await first.Transaction.CommitAsync(timeout.Token);
			var blocked = await waiting;
			Assert.NotNull(blocked);
			Assert.Equal(approvedId, blocked.RequestId);
			Assert.Equal(ConcessionRequestStatus.Approved, blocked.Status);
			Assert.Same(second.Outer, second.Current);
			Assert.Equal(isolation, second.Transaction.GetDbTransaction().IsolationLevel);
			if (isolation == IsolationLevel.Serializable)
				Assert.Null(await second.Repository.FindBlockingBenefitAsync(key.StudentId, key.TypeId, key.YearId));
			Assert.Equal(1L, await database.CountAsync(key));
			// Both writer-pool slots remain held; the guard must use an independent read connection.
			Assert.Equal(ConnectionState.Open, first.Outer.Database.GetDbConnection().State);
			Assert.Equal(ConnectionState.Open, second.Outer.Database.GetDbConnection().State);
		}
		finally
		{
			timeout.Cancel();
			try { await waiting; } catch (OperationCanceledException) { }
		}
	}

	[ConcessionBenefitPostgresFact]
	public async Task Fresh_committed_read_observes_replay_receipt_while_serializable_snapshot_stays_unchanged()
	{
		var key = BenefitKey.New();
		var replayKey = Guid.NewGuid();
		var savedRequestId = Guid.NewGuid();
		await using var first = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, IsolationLevel.ReadCommitted);
		await using var second = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, IsolationLevel.Serializable);
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
		Assert.Null(await second.ReadReplayAsync(replayKey));
		await first.Repository.AcquireBenefitLockAsync(key.StudentId, key.TypeId, key.YearId, timeout.Token);
		async Task<Guid?> WaitAndReplayAsync()
		{
			await second.Repository.AcquireBenefitLockAsync(key.StudentId, key.TypeId, key.YearId, timeout.Token);
			return await second.Repository.ReadCommittedAsync(() => second.ReadReplayAsync(replayKey), timeout.Token);
		}
		var waiting = WaitAndReplayAsync();
		try
		{
			await database.WaitForAdvisoryWaitAsync(second.ProcessId, waiting, timeout.Token);
			await ConcessionBenefitGuardDatabase.ExecuteAsync(first.Outer,
				"INSERT INTO \"BenefitGuardReplayProbe\" (\"Key\", \"RequestId\") VALUES (@key, @id)",
				new NpgsqlParameter("key", replayKey), new NpgsqlParameter("id", savedRequestId));
			await first.Transaction.CommitAsync(timeout.Token);
			Assert.Equal(savedRequestId, await waiting);
			Assert.Same(second.Outer, second.Current);
			Assert.Null(await second.ReadReplayAsync(replayKey));
			Assert.Equal(IsolationLevel.Serializable, second.Transaction.GetDbTransaction().IsolationLevel);
		}
		finally
		{
			timeout.Cancel();
			try { await waiting; } catch (OperationCanceledException) { }
		}
	}

	[ConcessionBenefitPostgresFact]
	public async Task Blocking_statuses_scopes_self_exclusion_and_legacy_approved_priority_are_enforced()
	{
		var key = BenefitKey.New();
		var statuses = Enum.GetValues<ConcessionRequestStatus>();
		var types = new Dictionary<ConcessionRequestStatus, Guid>();
		await using (var writer = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, IsolationLevel.ReadCommitted))
		{
			foreach (var status in statuses)
			{
				types[status] = Guid.NewGuid();
				await database.InsertAsync(writer.Outer, key with { TypeId = types[status] }, Guid.NewGuid(), status);
			}
			await database.InsertAsync(writer.Outer, key with { TenantId = Guid.NewGuid() }, Guid.NewGuid(), ConcessionRequestStatus.Approved);
			await database.InsertAsync(writer.Outer, key with { YearId = Guid.NewGuid() }, Guid.NewGuid(), ConcessionRequestStatus.Approved);
			await database.InsertAsync(writer.Outer, key with { StudentId = Guid.NewGuid() }, Guid.NewGuid(), ConcessionRequestStatus.Approved);
			await writer.Transaction.CommitAsync();
		}
		await using var reader = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, IsolationLevel.ReadCommitted);
		foreach (var status in statuses)
		{
			var result = await reader.Repository.FindBlockingBenefitAsync(key.StudentId, types[status], key.YearId);
			if (status is ConcessionRequestStatus.Pending or ConcessionRequestStatus.Approved or ConcessionRequestStatus.PartiallyReversed)
				Assert.Equal(status, Assert.IsType<ConcessionBenefitConflict>(result).Status);
			else Assert.Null(result);
		}
		Assert.Null(await reader.Repository.FindBlockingBenefitAsync(key.StudentId, key.TypeId, key.YearId));
		Assert.Equal(3, (await reader.Repository.GetBlockingBenefitsAsync(key.StudentId, key.YearId)).Count);
		var approvedId = Guid.NewGuid();
		var pendingId = Guid.NewGuid();
		await database.InsertAsync(reader.Outer, key, pendingId, ConcessionRequestStatus.Pending);
		await database.InsertAsync(reader.Outer, key, approvedId, ConcessionRequestStatus.Approved);
		Assert.Equal(approvedId, (await reader.Repository.FindBlockingBenefitAsync(key.StudentId, key.TypeId, key.YearId))!.RequestId);
		Assert.Equal(approvedId, (await reader.Repository.FindBlockingBenefitAsync(key.StudentId, key.TypeId, key.YearId, pendingId))!.RequestId);
		Assert.Equal(pendingId, (await reader.Repository.FindBlockingBenefitAsync(key.StudentId, key.TypeId, key.YearId, approvedId))!.RequestId);
	}

	[ConcessionBenefitPostgresTheory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task A_claim_in_the_same_uncommitted_transaction_blocks_before_and_after_flush(bool flushed)
	{
		var key = BenefitKey.New();
		await using var actor = await GuardActor.StartAsync(database.ConnectionString, key.TenantId, IsolationLevel.ReadCommitted);
		var trackedId = Guid.NewGuid();
		var request = new ConcessionRequest(trackedId, key.TenantId, key.StudentId, Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), key.TypeId, key.YearId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			null, ConcessionRequestStatus.Pending);
		actor.Outer.Add(request);
		if (flushed)
		{
			// Reproduce a completed EF flush without invoking unrelated ABP audit/event services.
			// SQL is real and uncommitted; AcceptAllChanges mirrors EF's post-save tracking state.
			await database.InsertAsync(actor.Outer, key, trackedId, ConcessionRequestStatus.Pending);
			actor.Outer.ChangeTracker.AcceptAllChanges();
			Assert.Equal(EntityState.Unchanged, actor.Outer.Entry(request).State);
		}
		var result = await actor.Repository.FindBlockingBenefitForMutationAsync(key.StudentId, key.TypeId, key.YearId);
		Assert.Equal(trackedId, Assert.IsType<ConcessionBenefitConflict>(result).RequestId);
		Assert.Null(await actor.Repository.FindBlockingBenefitForMutationAsync(key.StudentId, key.TypeId, key.YearId, trackedId));
		Assert.Equal(0L, await database.CountAsync(key));
	}

	internal sealed record BenefitKey(Guid TenantId, Guid StudentId, Guid YearId, Guid TypeId)
	{
		public static BenefitKey New() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
	}

	private sealed class GuardActor : IAsyncDisposable
	{
		private readonly string _connection;
		public ShikshaMitraDbContext Outer { get; }
		public ShikshaMitraDbContext Current { get; private set; }
		public IDbContextTransaction Transaction { get; private set; } = null!;
		public EfCoreConcessionRequestRepository Repository { get; }
		public int ProcessId => ((NpgsqlConnection)Outer.Database.GetDbConnection()).ProcessID;

		private GuardActor(string connection, Guid tenantId)
		{
			_connection = connection;
			Outer = NewContext();
			Current = Outer;
			var contexts = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
			contexts.GetDbContextAsync().Returns(_ => Task.FromResult(Current));
			var tenant = Substitute.For<ICurrentTenant>(); tenant.Id.Returns(tenantId);
			var units = Substitute.For<IUnitOfWorkManager>();
			units.Begin(Arg.Any<AbpUnitOfWorkOptions>(), true).Returns(call =>
			{
				Assert.False(call.ArgAt<AbpUnitOfWorkOptions>(0).IsTransactional);
				var previous = Current;
				var fresh = NewContext(); Current = fresh;
				var unit = Substitute.For<IUnitOfWork>();
				unit.CompleteAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
				unit.When(x => x.Dispose()).Do(_ => { fresh.Dispose(); Current = previous; });
				return unit;
			});
			var lazy = Substitute.For<IAbpLazyServiceProvider>();
			lazy.LazyGetRequiredService<ICurrentTenant>().Returns(tenant);
			lazy.LazyGetRequiredService<IUnitOfWorkManager>().Returns(units);
			Repository = new GuardProbeRepository(contexts) { LazyServiceProvider = lazy };
		}

		private ShikshaMitraDbContext NewContext() => new GuardProbeDbContext(
			new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(_connection).Options);

		public static async Task<GuardActor> StartAsync(string connection, Guid tenantId, IsolationLevel isolation)
		{
			var actor = new GuardActor(connection, tenantId);
			await actor.Outer.Database.OpenConnectionAsync();
			actor.Transaction = await actor.Outer.Database.BeginTransactionAsync(isolation);
			return actor;
		}

		public async Task<Guid?> ReadReplayAsync(Guid key)
		{
			await Current.Database.OpenConnectionAsync();
			await using var command = new NpgsqlCommand("SELECT \"RequestId\" FROM \"BenefitGuardReplayProbe\" WHERE \"Key\"=@key",
				(NpgsqlConnection)Current.Database.GetDbConnection(), (NpgsqlTransaction?)Current.Database.CurrentTransaction?.GetDbTransaction());
			command.Parameters.AddWithValue("key", key);
			return await command.ExecuteScalarAsync() is Guid id ? id : null;
		}

		public async ValueTask DisposeAsync()
		{
			await Transaction.DisposeAsync();
			await Outer.DisposeAsync();
		}
	}

	private sealed class GuardProbeRepository(IDbContextProvider<ShikshaMitraDbContext> contexts)
		: EfCoreConcessionRequestRepository(contexts)
	{
		public override async Task<IQueryable<ConcessionRequest>> GetQueryableAsync() =>
			(await GetDbContextAsync()).Set<ConcessionRequest>();
	}

	private sealed class GuardProbeDbContext(DbContextOptions<ShikshaMitraDbContext> options) : ShikshaMitraDbContext(options)
	{
		protected override void OnModelCreating(ModelBuilder model)
		{
			foreach (var entity in model.Model.GetEntityTypes().ToList()) model.Ignore(entity.ClrType);
			var request = model.Entity<ConcessionRequest>();
			var columns = new HashSet<string> { "Id", "TenantId", "StudentId", "AcademicYearId", "ConcessionTypeId", "Status", "IsDeleted", "CreationTime" };
			foreach (var property in typeof(ConcessionRequest).GetProperties())
				if (!columns.Contains(property.Name)) request.Ignore(property.Name);
			foreach (var entity in model.Model.GetEntityTypes().Where(x => x.ClrType != typeof(ConcessionRequest)).ToList()) model.Ignore(entity.ClrType);
			request.ToTable("Finance_ConcessionRequests"); request.HasKey(x => x.Id);
		}
	}
}

public sealed class ConcessionBenefitGuardDatabase : IAsyncLifetime
{
	public const string ConnectionVariable = "SHIKSHAMITRA_CONCESSION_BENEFIT_TEST_DB";
	private bool _created;
	public string ConnectionString { get; private set; } = string.Empty;
	private NpgsqlConnectionStringBuilder _settings = null!;

	public async Task InitializeAsync()
	{
		var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
		if (string.IsNullOrWhiteSpace(configured)) return;
		_settings = new NpgsqlConnectionStringBuilder(configured);
		if (_settings.Host is not ("localhost" or "127.0.0.1" or "::1") ||
			_settings.Database is null || !Regex.IsMatch(_settings.Database, "^concession_benefit_test_[a-z0-9_]{1,32}$"))
			throw new InvalidOperationException("Only a fresh disposable loopback concession_benefit_test_* database is allowed.");
		_settings.MaxPoolSize = 2; _settings.Timeout = 5;
		ConnectionString = _settings.ConnectionString;
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_settings.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
		await admin.OpenAsync();
		await using var exists = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname=@name)", admin);
		exists.Parameters.AddWithValue("name", _settings.Database);
		if ((bool)(await exists.ExecuteScalarAsync())!) throw new InvalidOperationException("Refusing to reuse/reset an existing database.");
		await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_settings.Database}\"", admin);
		await create.ExecuteNonQueryAsync(); _created = true;
		await using var schema = await OpenObserverAsync();
		await using var command = new NpgsqlCommand("""
			CREATE TABLE "Finance_ConcessionRequests" (
			  "Id" uuid PRIMARY KEY, "TenantId" uuid NULL, "StudentId" uuid NOT NULL,
			  "AcademicYearId" uuid NOT NULL, "ConcessionTypeId" uuid NOT NULL,
			  "Status" integer NOT NULL, "IsDeleted" boolean NOT NULL DEFAULT false,
			  "CreationTime" timestamp with time zone NOT NULL
			);
			CREATE TABLE "BenefitGuardReplayProbe" ("Key" uuid PRIMARY KEY, "RequestId" uuid NOT NULL);
			""", schema);
		await command.ExecuteNonQueryAsync();
	}

	private async Task<NpgsqlConnection> OpenObserverAsync()
	{
		var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString);
		await connection.OpenAsync(); return connection;
	}

	internal async Task InsertAsync(ShikshaMitraDbContext db, ConcessionBenefitGuardConcurrency_Tests.BenefitKey key, Guid id, ConcessionRequestStatus status) =>
		await ExecuteAsync(db, """
			INSERT INTO "Finance_ConcessionRequests" ("Id","TenantId","StudentId","AcademicYearId","ConcessionTypeId","Status","IsDeleted","CreationTime")
			VALUES (@id,@tenant,@student,@year,@type,@status,false,@created)
			""", new NpgsqlParameter("id", id), new NpgsqlParameter("tenant", key.TenantId), new NpgsqlParameter("student", key.StudentId),
			new NpgsqlParameter("year", key.YearId), new NpgsqlParameter("type", key.TypeId), new NpgsqlParameter("status", (int)status),
			new NpgsqlParameter("created", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow });

	internal static async Task ExecuteAsync(ShikshaMitraDbContext db, string sql, params NpgsqlParameter[] parameters)
	{
		await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)db.Database.GetDbConnection(),
			(NpgsqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction());
		command.Parameters.AddRange(parameters); await command.ExecuteNonQueryAsync();
	}

	internal async Task<long> CountAsync(ConcessionBenefitGuardConcurrency_Tests.BenefitKey key)
	{
		await using var connection = await OpenObserverAsync();
		await using var command = new NpgsqlCommand("SELECT count(*) FROM \"Finance_ConcessionRequests\" WHERE \"TenantId\"=@tenant AND \"StudentId\"=@student AND \"AcademicYearId\"=@year AND \"ConcessionTypeId\"=@type", connection);
		command.Parameters.AddWithValue("tenant", key.TenantId); command.Parameters.AddWithValue("student", key.StudentId);
		command.Parameters.AddWithValue("year", key.YearId); command.Parameters.AddWithValue("type", key.TypeId);
		return (long)(await command.ExecuteScalarAsync())!;
	}

	internal async Task WaitForAdvisoryWaitAsync(int pid, Task claimant, CancellationToken cancellation)
	{
		await using var observer = await OpenObserverAsync();
		await using var command = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid=@pid AND locktype='advisory' AND NOT granted)", observer);
		command.Parameters.AddWithValue("pid", pid);
		while (true)
		{
			if ((bool)(await command.ExecuteScalarAsync(cancellation))!) return;
			Assert.False(claimant.IsCompleted, "The competing claim completed before the first transaction released its benefit lock.");
			await Task.Delay(20, cancellation);
		}
	}

	public async Task DisposeAsync()
	{
		if (!_created) return;
		NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
		await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_settings.ConnectionString) { Database = "postgres", Pooling = false }.ConnectionString);
		await admin.OpenAsync();
		await using var drop = new NpgsqlCommand($"DROP DATABASE \"{_settings.Database}\" WITH (FORCE)", admin);
		await drop.ExecuteNonQueryAsync(); _created = false;
	}
}
