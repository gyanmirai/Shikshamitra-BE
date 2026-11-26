using System;
using System.Diagnostics;
using System.Threading;

namespace ShikshaMitra.Diagnostics;

/// <summary>Request-local counters only; never retains commands, parameters or application data.</summary>
public sealed class ApiRequestTimingScope : IDisposable
{
	private static readonly AsyncLocal<ApiRequestTimingScope?> Ambient = new();
	private readonly ApiRequestTimingScope? _previous;
	private readonly Stopwatch _watch = Stopwatch.StartNew();
	private long _commandTicks;
	private long _connectionTicks;
	private long _commandCount;
	private long _maxCommandTicks;
	private long _beforeAuthTicks = -1;
	private long _afterAuthTicks = -1;
	private long _beforeEndpointTicks = -1;

	internal void MarkBeforeAuth() => RecordCheckpoint(ref _beforeAuthTicks);
	internal void MarkAfterAuth() => RecordCheckpoint(ref _afterAuthTicks);
	internal void MarkBeforeEndpoint() => RecordCheckpoint(ref _beforeEndpointTicks);
	internal (double? BeforeAuth, double? Auth, double? AfterAuth, double? Endpoint) ReadPhases()
	{
		var end = _watch.Elapsed.Ticks;
		var beforeAuth = Interlocked.Read(ref _beforeAuthTicks);
		var afterAuth = Interlocked.Read(ref _afterAuthTicks);
		var beforeEndpoint = Interlocked.Read(ref _beforeEndpointTicks);
		return (Duration(0, beforeAuth), Duration(beforeAuth, afterAuth),
			Duration(afterAuth, beforeEndpoint), Duration(beforeEndpoint, end));
	}

	private void RecordCheckpoint(ref long checkpoint) =>
		Interlocked.CompareExchange(ref checkpoint, _watch.Elapsed.Ticks, -1);

	private static double? Duration(long start, long end) =>
		start >= 0 && end >= start ? TimeSpan.FromTicks(end - start).TotalMilliseconds : null;

	private ApiRequestTimingScope()
	{
		_previous = Ambient.Value;
		Ambient.Value = this;
	}

	public static ApiRequestTimingScope? Current => Ambient.Value;
	public static ApiRequestTimingScope Start() => new();
	public double ElapsedMilliseconds => _watch.Elapsed.TotalMilliseconds;
	public long CommandCount => Interlocked.Read(ref _commandCount);
	public double CommandMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref _commandTicks)).TotalMilliseconds;
	public double ConnectionMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref _connectionTicks)).TotalMilliseconds;
	public double MaxCommandMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref _maxCommandTicks)).TotalMilliseconds;

	public void RecordCommand(TimeSpan duration)
	{
		Interlocked.Increment(ref _commandCount);
		Interlocked.Add(ref _commandTicks, duration.Ticks);
		long previous;
		do
		{
			previous = Interlocked.Read(ref _maxCommandTicks);
			if (previous >= duration.Ticks) return;
		} while (Interlocked.CompareExchange(ref _maxCommandTicks, duration.Ticks, previous) != previous);
	}

	public void RecordConnection(TimeSpan duration) => Interlocked.Add(ref _connectionTicks, duration.Ticks);
	public void Dispose() => Ambient.Value = _previous;
}
