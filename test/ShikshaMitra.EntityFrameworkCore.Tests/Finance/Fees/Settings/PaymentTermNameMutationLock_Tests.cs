using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings;

public sealed class PaymentTermNamePostgresFactAttribute : FactAttribute
{
	public PaymentTermNamePostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FINANCE_PAYMENT_TERM_TEST_POSTGRES")))
			Skip = "Requires a loopback finance_name_lock_tests_* database in FINANCE_PAYMENT_TERM_TEST_POSTGRES; no schema or financial data is modified.";
	}
}

public class PaymentTermNameMutationLock_Tests
{
	[Fact]
	public async Task Name_mutations_require_a_transaction_before_acquiring_the_database_lock()
	{
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var context = CreateContext("Host=localhost;Database=unused;Username=unused;Password=unused");
		var mutationLock = CreateLock(context, Guid.NewGuid());
		var error = await Should.ThrowAsync<InvalidOperationException>(() => mutationLock.AcquireAsync(Guid.NewGuid()));
		error.Message.ShouldContain("transaction");
	}

	[PaymentTermNamePostgresFact]
	public async Task Ambient_serializable_transactions_are_rejected_before_a_stale_name_snapshot_can_be_used()
	{
		var connection = GetTestConnection();
		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var context = CreateContext(connection.ConnectionString);
		await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
		var error = await Should.ThrowAsync<InvalidOperationException>(() =>
			CreateLock(context, Guid.NewGuid()).AcquireAsync(Guid.NewGuid()));
		error.Message.ShouldContain("Read Committed");
	}

	[PaymentTermNamePostgresFact]
	public async Task Concurrent_name_writers_wait_for_commit_only_in_the_same_tenant_and_year()
	{
		var connection = GetTestConnection();

		ShikshaMitraEfCoreEntityExtensionMappings.Configure();
		await using var first = CreateContext(connection.ConnectionString);
		await using var second = CreateContext(connection.ConnectionString);
		await using var firstTransaction = await first.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
		await using var secondTransaction = await second.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
		var tenantId = Guid.NewGuid();
		var yearId = Guid.NewGuid();
		await CreateLock(first, tenantId).AcquireAsync(yearId);

		// Independent scopes proceed even while the first writer is still open.
		await CreateLock(second, tenantId).AcquireAsync(Guid.NewGuid()).WaitAsync(TimeSpan.FromSeconds(5));
		await CreateLock(second, Guid.NewGuid()).AcquireAsync(yearId).WaitAsync(TimeSpan.FromSeconds(5));
		await using var pidCommand = new NpgsqlCommand("SELECT pg_backend_pid()",
			(NpgsqlConnection)second.Database.GetDbConnection(), (NpgsqlTransaction)secondTransaction.GetDbTransaction());
		var secondPid = (int)(await pidCommand.ExecuteScalarAsync())!;
		var waitingWriter = CreateLock(second, tenantId).AcquireAsync(yearId);

		await using var observer = new NpgsqlConnection(connection.ConnectionString);
		await observer.OpenAsync();
		var deadline = DateTime.UtcNow.AddSeconds(5);
		var isWaiting = false;
		while (!isWaiting && DateTime.UtcNow < deadline)
		{
			await using var command = new NpgsqlCommand(
				"SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid = @pid AND locktype = 'advisory' AND NOT granted)", observer);
			command.Parameters.AddWithValue("pid", secondPid);
			isWaiting = (bool)(await command.ExecuteScalarAsync())!;
			if (!isWaiting) await Task.Delay(20);
		}
		isWaiting.ShouldBeTrue("A competing name writer must wait for the owning transaction, not only its lock method, to finish.");
		waitingWriter.IsCompleted.ShouldBeFalse();
		await firstTransaction.CommitAsync();
		await waitingWriter.WaitAsync(TimeSpan.FromSeconds(5));
		await secondTransaction.RollbackAsync();
	}

	private static NpgsqlConnectionStringBuilder GetTestConnection()
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FINANCE_PAYMENT_TERM_TEST_POSTGRES"));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("finance_name_lock_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Only a loopback finance_name_lock_tests_* database is allowed.");
		return connection;
	}

	private static ShikshaMitraDbContext CreateContext(string connection) =>
		new(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection).Options);

	private static PaymentTermNameMutationLock CreateLock(ShikshaMitraDbContext context, Guid tenantId) =>
		new(new ContextProvider(context), new Tenant(tenantId));

	private sealed class ContextProvider(ShikshaMitraDbContext context) : IDbContextProvider<ShikshaMitraDbContext>
	{
		public ShikshaMitraDbContext GetDbContext() => context;
		public Task<ShikshaMitraDbContext> GetDbContextAsync() => Task.FromResult(context);
	}

	private sealed class Tenant(Guid tenantId) : ICurrentTenant
	{
		public bool IsAvailable => true;
		public Guid? Id => tenantId;
		public string? Name => "Synthetic lock tenant";
		public IDisposable Change(Guid? id, string? name = null) => throw new NotSupportedException();
	}
}
