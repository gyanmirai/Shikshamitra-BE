using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.Academic.Timetable.Portal;

internal sealed class PortalProfileQueryObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
	private readonly IDisposable _all;
	private readonly List<IDisposable> _subscriptions = [];
	public List<string> Commands { get; } = [];
	public PortalProfileQueryObserver() => _all = DiagnosticListener.AllListeners.Subscribe(this);
	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name == "Microsoft.EntityFrameworkCore")
			_subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuting.Name));
	}
	public void OnNext(KeyValuePair<string, object?> value)
	{
		if (value.Value is CommandEventData command) Commands.Add(command.Command.CommandText);
	}
	public void OnCompleted() { }
	public void OnError(Exception error) { }
	public void Dispose() { _all.Dispose(); foreach (var item in _subscriptions) item.Dispose(); }
}
