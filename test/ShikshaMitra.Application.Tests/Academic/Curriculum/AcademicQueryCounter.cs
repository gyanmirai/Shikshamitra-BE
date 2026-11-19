using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.Academic.Curriculum;

// Scope counts to the test database connection so parallel tests cannot affect assertions.
internal sealed class AcademicQueryCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
	private readonly DbConnection _connection;
	private readonly IDisposable _listenerSubscription;
	private readonly ConcurrentBag<IDisposable> _subscriptions = new();
	private int _count;
	private int _readCount;
	private readonly ConcurrentBag<string> _readCommands = [];

	public AcademicQueryCounter(DbConnection connection)
	{
		_connection = connection;
		_listenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
	}

	public int Count => Volatile.Read(ref _count);
	public int ReadCount => Volatile.Read(ref _readCount);
	public IReadOnlyCollection<string> ReadCommands => _readCommands.ToArray();

	public void OnNext(DiagnosticListener listener)
	{
		if (listener.Name == "Microsoft.EntityFrameworkCore")
		{
			_subscriptions.Add(listener.Subscribe(this, name => name == RelationalEventId.CommandExecuted.Name));
		}
	}

	public void OnNext(KeyValuePair<string, object?> value)
	{
		if (value.Value is CommandExecutedEventData data && data.Context != null &&
			ReferenceEquals(data.Context.Database.GetDbConnection(), _connection))
		{
			Interlocked.Increment(ref _count);
			if (data.Command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
			{
				Interlocked.Increment(ref _readCount);
				_readCommands.Add(data.Command.CommandText);
			}
		}
	}

	public void OnCompleted() { }
	public void OnError(Exception error) { }

	public void Dispose()
	{
		_listenerSubscription.Dispose();
		foreach (var subscription in _subscriptions)
		{
			subscription.Dispose();
		}
	}
}
