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
