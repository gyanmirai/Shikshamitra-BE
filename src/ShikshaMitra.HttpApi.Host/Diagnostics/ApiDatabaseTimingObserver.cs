using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace ShikshaMitra.Diagnostics;

/// <summary>Uses EF's existing events without enabling SQL or sensitive-data logging.</summary>
public sealed class ApiDatabaseTimingObserver : IHostedService, IObserver<DiagnosticListener>,
	IObserver<KeyValuePair<string, object?>>
{
	private readonly List<IDisposable> _subscriptions = [];
	private IDisposable? _listeners;
	private readonly object _gate = new();
	private bool _stopped;
	private const string Executed = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";
	private const string Failed = "Microsoft.EntityFrameworkCore.Database.Command.CommandError";
	private const string Opened = "Microsoft.EntityFrameworkCore.Database.Connection.ConnectionOpened";

	public Task StartAsync(CancellationToken cancellationToken)
	{
		_listeners = DiagnosticListener.AllListeners.Subscribe(this);
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		_listeners?.Dispose();
		lock (_gate)
		{
			_stopped = true;
			foreach (var subscription in _subscriptions) subscription.Dispose();
			_subscriptions.Clear();
		}
		return Task.CompletedTask;
	}

	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name != "Microsoft.EntityFrameworkCore") return;
		lock (_gate)
		{
			if (!_stopped)
				_subscriptions.Add(listener.Subscribe(this, name => name is Executed or Failed or Opened));
		}
	}

	public void OnNext(KeyValuePair<string, object?> value)
	{
		var scope = ApiRequestTimingScope.Current;
		if (scope == null) return;
		if (value.Key is Executed or Failed && value.Value is CommandEndEventData command)
			scope.RecordCommand(command.Duration);
		else if (value.Key == Opened && value.Value is ConnectionEndEventData connection)
			scope.RecordConnection(connection.Duration);
	}

	public void OnCompleted() { }
	public void OnError(Exception error) { }
}
