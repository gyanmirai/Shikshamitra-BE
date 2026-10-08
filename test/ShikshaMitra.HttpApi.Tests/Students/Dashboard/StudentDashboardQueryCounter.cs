using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.Students.Dashboard;

public sealed class StudentDashboardQueryCounter : DbCommandInterceptor
{
	private readonly object _gate = new();
	private List<string>? _commands;
	public void Begin() { lock (_gate) _commands = []; }
	public List<string> End() { lock (_gate) { var commands = _commands ?? []; _commands = null; return commands; } }
	private void Observe(DbCommand command) { lock (_gate) _commands?.Add(command.CommandText); }
	public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
	{ Observe(command); return result; }
	public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
	{ Observe(command); return ValueTask.FromResult(result); }
}
