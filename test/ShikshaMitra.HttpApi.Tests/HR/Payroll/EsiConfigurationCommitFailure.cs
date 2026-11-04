using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.HR.Payroll;

// Fail only an explicitly armed transaction in the isolated historical-configuration fixture.
// Inspect the transaction itself to prove both deletes executed before inducing rollback.
public sealed class EsiConfigurationCommitFailure : DbTransactionInterceptor
{
	private int _armed;
	private Guid _employee;
	public bool ObservedDeletedSnapshot { get; private set; }
	public bool ObservedDeletedReview { get; private set; }
	public void Arm(Guid employee)
	{
		_employee = employee;
		ObservedDeletedSnapshot = false;
		ObservedDeletedReview = false;
		Interlocked.Exchange(ref _armed, 1);
	}
	public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
		TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
	{
		if (Interlocked.Exchange(ref _armed, 0) != 1) return result;
		await using var command = transaction.Connection!.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = "SELECT " +
			"(SELECT count(*) FROM \"HR_PayrollRunItems\" WHERE \"EmployeeId\" = @employee AND \"IsDeleted\"), " +
			"(SELECT count(*) FROM \"HR_PayrollRunReviews\" WHERE \"EmployeeId\" = @employee AND \"IsDeleted\")";
		var parameter = command.CreateParameter();
		parameter.ParameterName = "employee";
		parameter.Value = _employee;
		command.Parameters.Add(parameter);
		await using (var reader = await command.ExecuteReaderAsync(cancellationToken)) {
			await reader.ReadAsync(cancellationToken);
			ObservedDeletedSnapshot = reader.GetInt64(0) > 0;
			ObservedDeletedReview = reader.GetInt64(1) > 0;
		}
		throw new InvalidOperationException("Synthetic ESIC failure after snapshot/review writes and before commit.");
	}
}
