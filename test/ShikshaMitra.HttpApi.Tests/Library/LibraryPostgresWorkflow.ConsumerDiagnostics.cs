using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Xunit.Abstractions;

namespace ShikshaMitra.Library;

public sealed partial class LibraryPostgresWorkflow_Tests
{
	private readonly ITestOutputHelper consumerOutput;
	public LibraryPostgresWorkflow_Tests(ITestOutputHelper output) => consumerOutput = output;

	private async Task ExplainConsumerPgAsync(ShikshaMitraDbContext db,
		FinanceConsumerPgCapturedCommand[] captured, string stage)
	{
		for (var index = 0; index < captured.Length; index++)
		{
			var source = captured[index];
			await using var command = db.Database.GetDbConnection().CreateCommand();
			command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
			command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + source.Sql;
			command.CommandTimeout = db.Database.GetCommandTimeout() ?? 30;
			foreach (var parameter in source.Parameters)
				command.Parameters.Add(((ICloneable)parameter).Clone());
			var watch = Stopwatch.StartNew();
			var planText = (string)(await command.ExecuteScalarAsync())!;
			using var plan = JsonDocument.Parse(planText);
			var root = plan.RootElement[0];
			root.GetProperty("Planning Time").GetDecimal().ShouldBeGreaterThanOrEqualTo(0m);
			root.GetProperty("Execution Time").GetDecimal().ShouldBeGreaterThanOrEqualTo(0m);
			consumerOutput.WriteLine(JsonSerializer.Serialize(new
			{
				Stage = stage, Operation = index, SqlLength = source.Sql.Length,
				ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds,
				PlanningMilliseconds = root.GetProperty("Planning Time").GetDecimal(),
				ExecutionMilliseconds = root.GetProperty("Execution Time").GetDecimal(),
				Sql = source.Sql, Plan = root
			}));
		}
	}
}

internal sealed record FinanceConsumerPgCapturedCommand(string Sql, NpgsqlParameter[] Parameters, int CommandTimeout);

internal sealed class FinanceConsumerPgSqlCapture
	: IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
	private readonly ActivityTraceId traceId;
	private readonly bool captureAnySourceOrFinance;
	private readonly ConcurrentBag<IDisposable> subscriptions = [];
	private readonly IDisposable listeners;
	public ConcurrentQueue<FinanceConsumerPgCapturedCommand> Commands { get; } = new();

	public FinanceConsumerPgSqlCapture(ActivityTraceId traceId, bool captureAnySourceOrFinance = false)
	{
		this.traceId = traceId;
		this.captureAnySourceOrFinance = captureAnySourceOrFinance;
		listeners = DiagnosticListener.AllListeners.Subscribe(this);
	}

	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name == "Microsoft.EntityFrameworkCore") subscriptions.Add(listener.Subscribe(this));
	}
	public void OnNext(KeyValuePair<string, object?> item)
	{
		if (Activity.Current?.TraceId != traceId || !item.Key.EndsWith(".CommandExecuting", StringComparison.Ordinal) ||
			item.Value is not CommandEventData data) return;
		var sql = data.Command.CommandText;
		var source = sql.Contains("\"AppLibraryCharges\"", StringComparison.Ordinal);
		var finance = sql.Contains("\"Finance_", StringComparison.Ordinal);
		if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
			(captureAnySourceOrFinance ? !source && !finance : !source || !finance)) return;
		Commands.Enqueue(new(sql, data.Command.Parameters.Cast<DbParameter>()
			.Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray(), data.Command.CommandTimeout));
	}
	public void OnCompleted() { }
	public void OnError(Exception error) { }
	public void Dispose()
	{
		listeners.Dispose();
		foreach (var subscription in subscriptions) subscription.Dispose();
	}
}
