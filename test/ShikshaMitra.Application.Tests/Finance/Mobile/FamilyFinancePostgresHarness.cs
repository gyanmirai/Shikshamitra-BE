using System;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra;

public sealed class FamilyFinancePostgresFactAttribute : FactAttribute
{
    public FamilyFinancePostgresFactAttribute()
    {
        if (!FamilyFinancePostgresHarness.IsEnabled) Skip = "Requires isolated FAMILY_FINANCE_TEST_POSTGRES loopback cluster.";
    }
}

/// <summary>Opt-in only; creates a new database and never reuses, resets, or opens an existing application database.</summary>
internal static class FamilyFinancePostgresHarness
{
    public const string ConnectionVariable = "FAMILY_FINANCE_TEST_POSTGRES";
    public static bool IsEnabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    public static void Configure(IServiceCollection services)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
        if (connection.Host != "127.0.0.1" || connection.Port != 55438 || connection.Database != "family_finance_tests" || connection.Username != "family_tests")
            throw new InvalidOperationException("Family PostgreSQL tests require the dedicated loopback:55438 family_finance_tests cluster/user.");
        connection.Database = "family_finance_tests_" + Guid.NewGuid().ToString("N");
        var administrator = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres" };
        using (var sql = new NpgsqlConnection(administrator.ConnectionString))
        {
            sql.Open();
            using var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", sql);
            create.ExecuteNonQuery();
        }
        // Historic fixture constructors intentionally contain unspecified school calendar dates.
        var barrier = new FamilyFinancePostgresBarrier();
        services.AddSingleton(barrier);
        services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
            context.DbContextOptions.UseNpgsql(connection.ConnectionString).AddInterceptors(barrier)));
        using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(connection.ConnectionString).Options);
        database.GetService<IRelationalDatabaseCreator>().CreateTables();
    }
}

/// <summary>Synchronizes two real database commands without changing SQL or injecting provider failures.</summary>
public sealed class FamilyFinancePostgresBarrier : DbCommandInterceptor
{
    private int _armed;
    private int _arrived;
    private TaskCompletionSource _reached = Signal();
    private TaskCompletionSource _release = Signal();
    public ConcurrentDictionary<int, IsolationLevel> Connections { get; } = new();
    public ConcurrentDictionary<string, byte> Failures { get; } = new();
    public Task Reached => _reached.Task;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Arm()
    {
        _arrived = 0; Connections.Clear(); Failures.Clear(); _reached = Signal(); _release = Signal();
        Interlocked.Exchange(ref _armed, 1);
    }
    public void Release() { Interlocked.Exchange(ref _armed, 0); _release.TrySetResult(); }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _armed) == 1 && command.CommandText.Contains("Finance_FinanceReceiptNumberSequences", StringComparison.Ordinal)
            && (command.CommandText.StartsWith("INSERT", StringComparison.Ordinal) || command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)))
        {
            if (command.Transaction?.IsolationLevel != IsolationLevel.Serializable)
                throw new InvalidOperationException("Family reservations require real serializable transactions.");
            Connections.TryAdd(((NpgsqlConnection)command.Connection!).ProcessID, command.Transaction.IsolationLevel);
            if (Interlocked.Increment(ref _arrived) == 2) _reached.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
        return result;
    }
    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        for (Exception? error = eventData.Exception; error != null; error = error.InnerException)
            if (error is PostgresException postgres) Failures.TryAdd(postgres.SqlState, 0);
        return Task.CompletedTask;
    }
}
