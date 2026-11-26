using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using ShikshaMitra.HR.Payroll.RunPayroll;

namespace ShikshaMitra.Diagnostics;

/// <summary>Collects EF events and payroll checkpoints without retaining sensitive data.</summary>
public sealed class ApiRequestTimingObserver : IHostedService, IObserver<DiagnosticListener>,
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
		if (listener.Name is not ("Microsoft.EntityFrameworkCore" or "Microsoft.AspNetCore" or PayrollReadDiagnostics.SourceName)) return;
		lock (_gate)
		{
			if (!_stopped)
				_subscriptions.Add(listener.Subscribe(this, IsObservedEvent));
		}
	}

	private static bool IsObservedEvent(string name) => name switch
	{
		Executed or Failed or Opened or PayrollReadDiagnostics.CheckpointEvent => true,
		BeforeActionEventData.EventName or BeforeActionFilterOnActionExecutingEventData.EventName or
			BeforeActionFilterOnActionExecutionEventData.EventName => ApiRequestTimingScope.Current?.TracksPayrollRead == true,
		_ => false
	};

	public void OnNext(KeyValuePair<string, object?> value)
	{
		var scope = ApiRequestTimingScope.Current;
		if (scope == null) return;
		if (value.Key is Executed or Failed && value.Value is CommandEndEventData command)
			scope.RecordCommand(command.Duration);
		else if (value.Key == Opened && value.Value is ConnectionEndEventData connection)
			scope.RecordConnection(connection.Duration);
		else if (value.Key == PayrollReadDiagnostics.CheckpointEvent && value.Value is PayrollReadCheckpoint checkpoint)
			scope.RecordPayrollCheckpoint(checkpoint);
		else if (value.Key == BeforeActionEventData.EventName)
			scope.RecordPayrollCheckpoint(PayrollReadCheckpoint.MvcActionInvoking);
		else if (value.Key is BeforeActionFilterOnActionExecutingEventData.EventName or BeforeActionFilterOnActionExecutionEventData.EventName)
			scope.RecordPayrollCheckpoint(PayrollReadCheckpoint.ActionFiltersStarted);
	}

	public void OnCompleted() { }
	public void OnError(Exception error) { }
}
