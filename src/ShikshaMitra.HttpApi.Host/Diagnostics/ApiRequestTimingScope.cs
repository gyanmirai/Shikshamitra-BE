using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using ShikshaMitra.HR.Payroll.RunPayroll;

namespace ShikshaMitra.Diagnostics;

/// <summary>Request-local counters only; never retains commands, parameters or application data.</summary>
public sealed class ApiRequestTimingScope : IDisposable
{
	private static readonly AsyncLocal<ApiRequestTimingScope?> Ambient = new();
	private readonly ApiRequestTimingScope? _previous;
	private readonly Stopwatch _watch = Stopwatch.StartNew();
	private readonly long[]? _payrollCheckpoints;
	private long _commandTicks;
	private long _connectionTicks;
	private long _commandCount;
	private long _maxCommandTicks;

	private ApiRequestTimingScope(bool trackPayrollRead)
	{
		if (trackPayrollRead)
			_payrollCheckpoints = new long[(int)PayrollReadCheckpoint.ActionCompleted + 1];
		_previous = Ambient.Value;
		Ambient.Value = this;
	}

	public static ApiRequestTimingScope? Current => Ambient.Value;
	public static ApiRequestTimingScope Start(bool trackPayrollRead = false) => new(trackPayrollRead);
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

	public void RecordPayrollCheckpoint(PayrollReadCheckpoint checkpoint)
	{
		if (_payrollCheckpoints != null)
			Interlocked.CompareExchange(ref _payrollCheckpoints[(int)checkpoint], _watch.Elapsed.Ticks + 1, 0);
	}

	public string? FormatPayrollPhases()
	{
		var checkpoints = _payrollCheckpoints;
		if (checkpoints == null || Interlocked.Read(ref checkpoints[(int)PayrollReadCheckpoint.ActionStarted]) == 0)
			return null;

		List<string> metrics = [];
		var headersAt = _watch.Elapsed.Ticks;
		long previous = 0;
		for (var index = 0; index < checkpoints.Length; index++)
		{
			var ticks = Interlocked.Read(ref checkpoints[index]) - 1;
			if (ticks < previous || ticks > headersAt)
				continue;

			var phase = (PayrollReadCheckpoint)index switch
			{
				PayrollReadCheckpoint.RoutingCompleted => "payroll-routing",
				PayrollReadCheckpoint.AuthenticationCompleted => "payroll-authentication",
				PayrollReadCheckpoint.PermissionCheckStarted => "payroll-policy-resolution",
				PayrollReadCheckpoint.PermissionIdentityResolved => "payroll-permission-identity",
				PayrollReadCheckpoint.PermissionProvidersChecked => "payroll-permission-providers",
				PayrollReadCheckpoint.AuthorizationCompleted => "payroll-authorization-end",
				PayrollReadCheckpoint.ActionStarted => "payroll-mvc-entry",
				PayrollReadCheckpoint.ServiceStarted => "payroll-service-entry",
				PayrollReadCheckpoint.AcademicYearResolved => "payroll-academic-year",
				PayrollReadCheckpoint.StaffScopeResolved => "payroll-staff-scope",
				PayrollReadCheckpoint.PreviewResolved => "payroll-preview",
				PayrollReadCheckpoint.CountRead => "payroll-count",
				PayrollReadCheckpoint.PageRead => "payroll-page",
				PayrollReadCheckpoint.DetailsRead => "payroll-details",
				PayrollReadCheckpoint.StatusRead => "payroll-status",
				_ => "payroll-action-exit"
			};
			metrics.Add(FormatMetric(phase, ticks - previous));
			previous = ticks;
		}
		metrics.Add(FormatMetric("payroll-response-headers", headersAt - previous));
		return string.Join(", ", metrics);
	}

	private static string FormatMetric(string name, long ticks) =>
		string.Format(CultureInfo.InvariantCulture, "{0};dur={1:F2}", name, TimeSpan.FromTicks(ticks).TotalMilliseconds);

	public void Dispose() => Ambient.Value = _previous;
}
