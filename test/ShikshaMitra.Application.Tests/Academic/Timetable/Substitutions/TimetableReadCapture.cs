using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.Academic.Timetable.Substitutions;

internal sealed class TimetableReadCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
	private readonly DbConnection _connection;
	private readonly IDisposable _all;
	private readonly ConcurrentBag<IDisposable> _subscriptions = [];
	private readonly ConcurrentBag<string> _commands = [];
	private readonly ConcurrentBag<(string Sql, int Count)> _reads = [];
	public int Count => _commands.Count;
	public IReadOnlyList<string> SelectStatements => _commands.Where(sql => sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)).ToArray();
	public TimetableReadCapture(DbConnection connection)
	{
		_connection = connection;
		_all = DiagnosticListener.AllListeners.Subscribe(this);
	}
	public int CommandsFor(string table) => _commands.Count(sql => sql.Contains(table, StringComparison.Ordinal));
	public int ReadsFor(string table) => _reads.Where(read => read.Sql.Contains(table, StringComparison.Ordinal)).Sum(read => read.Count);
	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name == "Microsoft.EntityFrameworkCore")
			_subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name ||
				name == RelationalEventId.DataReaderDisposing.Name));
	}
	public void OnNext(KeyValuePair<string, object?> value)
	{
		if (value.Value is CommandExecutedEventData command && command.Context != null &&
			ReferenceEquals(command.Context.Database.GetDbConnection(), _connection))
			_commands.Add(command.Command.CommandText);
		if (value.Value is DataReaderDisposingEventData read && read.Context != null &&
			ReferenceEquals(read.Context.Database.GetDbConnection(), _connection))
			_reads.Add((read.Command.CommandText, read.ReadCount));
	}
	public void OnCompleted() { }
	public void OnError(Exception error) { }
	public void Dispose()
	{
		_all.Dispose();
		foreach (var subscription in _subscriptions) subscription.Dispose();
	}
}
