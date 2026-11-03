using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.Finance.Fees.Settings;

// Observe real SQL, scoped to this fixture's connection so parallel tests cannot interfere.
internal sealed class FinanceSettingsReadCapture : IObserver<DiagnosticListener>,
	IObserver<KeyValuePair<string, object?>>, IDisposable
{
	private readonly DbConnection connection;
	private readonly IDisposable listener;
	private readonly ConcurrentBag<IDisposable> subscriptions = [];
	private readonly ConcurrentQueue<string> commands = [];

	public FinanceSettingsReadCapture(DbConnection connection)
	{
		this.connection = connection;
		listener = DiagnosticListener.AllListeners.Subscribe(this);
	}

	public string[] Commands => commands.ToArray();
	public int Count => commands.Count;

	public void OnNext(DiagnosticListener value)
	{
		if (value.Name == "Microsoft.EntityFrameworkCore")
			subscriptions.Add(value.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
	}

	public void OnNext(KeyValuePair<string, object?> value)
	{
		if (value.Value is CommandExecutedEventData data && data.Context != null &&
			ReferenceEquals(data.Context.Database.GetDbConnection(), connection) &&
			data.Command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
			commands.Enqueue(data.Command.CommandText);
	}

	public void OnCompleted() { }
	public void OnError(Exception error) { }
	public void Dispose()
	{
		listener.Dispose();
		foreach (var subscription in subscriptions) subscription.Dispose();
	}
}
