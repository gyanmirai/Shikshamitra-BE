using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;

namespace ShikshaMitra.Infrastructure.Data;

internal sealed class SelectCommandCapture : IObserver<DiagnosticListener>,
	IObserver<KeyValuePair<string, object?>>, IDisposable
{
	private readonly IDisposable _all;
	private readonly List<IDisposable> _subscriptions = [];
	private readonly string _table;

	public SelectCommandCapture(string table)
	{
		_table = table;
		_all = DiagnosticListener.AllListeners.Subscribe(this);
	}

	public ConcurrentQueue<string> Commands { get; } = new();

	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name == "Microsoft.EntityFrameworkCore")
			_subscriptions.Add(listener.Subscribe(this));
	}

	public void OnNext(KeyValuePair<string, object?> value)
	{
		if (value.Key != RelationalEventId.CommandExecuted.Name || value.Value is not CommandExecutedEventData data) return;
		var sql = data.Command.CommandText;
		if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
			&& sql.Contains(_table, StringComparison.Ordinal))
			Commands.Enqueue(sql);
	}

	public void OnCompleted() { }
	public void OnError(Exception error) { }

	public void Dispose()
	{
		_all.Dispose();
		foreach (var subscription in _subscriptions) subscription.Dispose();
	}
}
