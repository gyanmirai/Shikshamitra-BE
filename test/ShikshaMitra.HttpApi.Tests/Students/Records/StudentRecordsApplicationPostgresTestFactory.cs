using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordsApplicationPostgresFactAttribute : FactAttribute
{
	public StudentRecordsApplicationPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(StudentRecordsApplicationPostgresTestFactory.ConnectionVariable)))
			Skip = "Requires a fresh loopback records_tests_* database name in STUDENT_RECORDS_APPLICATION_POSTGRES.";
	}
}

/// <summary>Real application HTTP requests; only a new disposable local database may be created.</summary>
public class StudentRecordsApplicationPostgresTestFactory : ShikshaMitraApiTestFactory
{
	public const string ConnectionVariable = "STUDENT_RECORDS_APPLICATION_POSTGRES";
	public StudentRecordsInsertBarrier Barrier { get; } = new();
	public string ConnectionString { get; private set; } = "";
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["ConnectionStrings:Default"] = Environment.GetEnvironmentVariable(ConnectionVariable)
		}));
	}
	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") || connection.Database == null ||
			!Regex.IsMatch(connection.Database, "^records_tests_[a-z0-9_]{1,48}$", RegexOptions.CultureInvariant))
			throw new InvalidOperationException("Only a fresh disposable loopback records_tests_* database is allowed.");
		ConnectionString = connection.ConnectionString;
		var administrator = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };
		using (var sql = new NpgsqlConnection(administrator.ConnectionString))
		{
			sql.Open();
			using var existing = new NpgsqlCommand("SELECT count(*) FROM pg_database WHERE datname = @name", sql);
			existing.Parameters.AddWithValue("name", connection.Database);
			if ((long)existing.ExecuteScalar()! != 0) throw new InvalidOperationException("Refusing to reuse or reset an existing PostgreSQL database.");
			using var create = new NpgsqlCommand($"CREATE DATABASE \"{connection.Database}\"", sql); create.ExecuteNonQuery();
		}
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(ConnectionString, provider => provider.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
				.AddInterceptors(Barrier, new StudentRecordsTransactionFailureObserver(Barrier))));
		using var database = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(ConnectionString).Options);
		// Model creation is intentional: this gate proves application behavior, not migration history.
		database.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
}

public sealed class StudentRecordsTransactionFailureObserver(StudentRecordsInsertBarrier barrier) : DbTransactionInterceptor
{
	public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
	{
		barrier.Capture(eventData.Exception); return Task.CompletedTask;
	}
}

/// <summary>Synchronize real SQL writes; never inject a provider exception or alter a command.</summary>
public sealed class StudentRecordsInsertBarrier : DbCommandInterceptor
{
	private int _armed;
	private int _arrived;
	private string _writeSql = "INSERT INTO \"AppStudentRecordTransitions\"";
	private IsolationLevel _isolation = IsolationLevel.Serializable;
	private TaskCompletionSource _reached = Signal();
	private TaskCompletionSource _release = Signal();
	public ConcurrentDictionary<int, byte> Connections { get; } = new();
	public ConcurrentDictionary<string, byte> ProviderFailures { get; } = new();
	public Task Reached => _reached.Task;
	public void Arm(string writeSql = "INSERT INTO \"AppStudentRecordTransitions\"", IsolationLevel isolation = IsolationLevel.Serializable)
	{
		_writeSql = writeSql; _isolation = isolation; _arrived = 0; Connections.Clear(); ProviderFailures.Clear();
		_reached = Signal(); _release = Signal(); Interlocked.Exchange(ref _armed, 1);
	}
	public void Release() { Interlocked.Exchange(ref _armed, 0); _release.TrySetResult(); }
	private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
	public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
	{
		if (Volatile.Read(ref _armed) == 1 && command.CommandText.Contains(_writeSql, StringComparison.Ordinal))
		{
			if (command.Transaction?.IsolationLevel != _isolation) throw new InvalidOperationException("Records concurrency writes must use the application's expected transaction isolation.");
			Connections.TryAdd(((NpgsqlConnection)command.Connection!).ProcessID, 0);
			if (Interlocked.Increment(ref _arrived) == 2) _reached.TrySetResult();
			await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
		}
		return result;
	}
	public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
	{
		Capture(eventData.Exception); return Task.CompletedTask;
	}
	public void Capture(Exception exception)
	{
		for (Exception? item = exception; item != null; item = item.InnerException)
			if (item is PostgresException postgres) ProviderFailures.TryAdd(postgres.SqlState, 0);
	}
}
