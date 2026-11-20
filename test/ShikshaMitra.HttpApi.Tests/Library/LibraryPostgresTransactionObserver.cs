using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace ShikshaMitra.Library;

internal sealed record LibraryPostgresCommandObservation(string Kind, bool HasTransaction, IsolationLevel? Isolation, int BackendId);

internal sealed class LibraryPostgresTransactionObserver : DbCommandInterceptor
{
	private readonly ConcurrentQueue<LibraryPostgresCommandObservation> observations = new();
	private int observing;
	public IReadOnlyList<LibraryPostgresCommandObservation> Observations => observations.ToArray();

	public void BeginObserving()
	{
		observations.Clear();
		Interlocked.Exchange(ref observing, 1);
	}

	public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
	{
		Observe(command);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		Observe(command);
		return ValueTask.FromResult(result);
	}

	public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
		InterceptionResult<DbDataReader> result)
	{
		Observe(command);
		return result;
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
		InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
	{
		Observe(command);
		return ValueTask.FromResult(result);
	}

	private void Observe(DbCommand command)
	{
		if (Volatile.Read(ref observing) == 0) return;
		foreach (var (fragment, kind) in new[]
		{
			("pg_advisory_xact_lock", "InventoryLock"),
			("INSERT INTO \"AppBookIssues\"", "LoanInsert"),
			("UPDATE \"AppBookIssues\"", "LoanUpdate"),
			("INSERT INTO \"AppLibraryCopyEvents\"", "CopyEventInsert"),
			("INSERT INTO \"AppLibraryLoanEvents\"", "LoanEventInsert"),
			("INSERT INTO \"AppLibraryLoanCommands\"", "CommandInsert")
		}.Where(item => command.CommandText.Contains(item.Item1, StringComparison.Ordinal)))
			observations.Enqueue(new(kind, command.Transaction != null, command.Transaction?.IsolationLevel,
				command.Connection is NpgsqlConnection postgres ? postgres.ProcessID : 0));
	}
}
