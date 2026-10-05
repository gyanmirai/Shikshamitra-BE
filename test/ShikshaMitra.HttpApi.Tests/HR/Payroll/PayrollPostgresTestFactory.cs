using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.HR.Payroll;

public sealed class PayrollPostgresFactAttribute : FactAttribute
{
	public PayrollPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_TEST_POSTGRES")))
			Skip = "Set PAYROLL_TEST_POSTGRES to an empty, disposable loopback payroll_tests_* PostgreSQL database.";
	}
}

public class PayrollPostgresTestFactory : ShikshaMitraApiTestFactory
{
	protected virtual string ConnectionVariable => "PAYROLL_TEST_POSTGRES";
	public PayrollCalculationBarrier Barrier { get; } = new();

	protected override void ConfigureTestDatabase(IServiceCollection services)
	{
		var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(ConnectionVariable));
		if (connection.Host is not ("127.0.0.1" or "localhost" or "::1") ||
			connection.Database?.StartsWith("payroll_tests_", StringComparison.Ordinal) != true)
			throw new InvalidOperationException("Payroll tests require a disposable loopback payroll_tests_* database.");
		services.Configure<AbpDbContextOptions>(options => options.Configure(context =>
			context.DbContextOptions.UseNpgsql(connection.ConnectionString).AddInterceptors(Barrier)));
		var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql(connection.ConnectionString).Options;
		using var database = new ShikshaMitraDbContext(options);
		// Intentionally fails when tables already exist. Never reset or delete an existing database.
		database.GetService<IRelationalDatabaseCreator>().CreateTables();
	}
}

public sealed class PayrollWorkflowPostgresTestFactory : PayrollPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_WORKFLOW_TEST_POSTGRES";
}

public sealed class PayrollRegistrationPostgresTestFactory : PayrollPostgresTestFactory
{
	protected override string ConnectionVariable => "PAYROLL_REGISTRATION_TEST_POSTGRES";
}

public sealed class PayrollRegistrationPostgresFactAttribute : FactAttribute
{
	public PayrollRegistrationPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_REGISTRATION_TEST_POSTGRES")))
			Skip = "Set PAYROLL_REGISTRATION_TEST_POSTGRES to a separate empty, disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayrollWorkflowPostgresFactAttribute : FactAttribute
{
	public PayrollWorkflowPostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYROLL_WORKFLOW_TEST_POSTGRES")))
			Skip = "Set PAYROLL_WORKFLOW_TEST_POSTGRES to a separate empty, disposable loopback payroll_tests_* database.";
	}
}

public sealed class PayrollCalculationBarrier : DbCommandInterceptor
{
	private int _armed;
	private string _commandFragment = "AppEmployeeAttendances";
	private int _failLeaseRead;
	public void FailNextLeaseRead() => Interlocked.Exchange(ref _failLeaseRead, 1);
	private TaskCompletionSource _reached = NewSignal();
	private TaskCompletionSource _release = NewSignal();
	private TaskCompletionSource _observed = NewSignal();
	private string? _observedFragment;
	private int _observationsRemaining;
	public Task Observed => _observed.Task;
	public void Observe(string commandFragment, int count = 1)
	{
		_observed = NewSignal();
		_observedFragment = commandFragment;
		Interlocked.Exchange(ref _observationsRemaining, count);
	}
	private void ObserveCommand(DbCommand command)
	{
		if (_observedFragment != null && command.CommandText.Contains(_observedFragment, StringComparison.Ordinal) &&
			Interlocked.Decrement(ref _observationsRemaining) == 0)
			_observed.TrySetResult();
	}
	public Task Reached => _reached.Task;
	public void Arm(string commandFragment = "AppEmployeeAttendances")
	{
		_commandFragment = commandFragment;
		_reached = NewSignal();
		_release = NewSignal();
		Interlocked.Exchange(ref _armed, 1);
	}
	public void Release() => _release.TrySetResult();
	private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

	public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
		DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
		CancellationToken cancellationToken = default)
	{
		ObserveCommand(command);
		if (command.CommandText.Contains("HR_PayrollMonthLocks", StringComparison.Ordinal) &&
			Interlocked.CompareExchange(ref _failLeaseRead, 0, 1) == 1)
			throw new NpgsqlException("Synthetic database connection failure");
		if (command.CommandText.Contains(_commandFragment, StringComparison.Ordinal) &&
			Interlocked.CompareExchange(ref _armed, 0, 1) == 1)
		{
			_reached.TrySetResult();
			await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
		}
		return result;
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
		DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		ObserveCommand(command);
		return ValueTask.FromResult(result);
	}
}