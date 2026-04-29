using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.HealthChecks;

public class PayrollSnapshotSchemaHealthCheck : IHealthCheck
{
	private readonly ShikshaMitraDbContext _dbContext;

	public PayrollSnapshotSchemaHealthCheck(ShikshaMitraDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await using var connection = _dbContext.Database.GetDbConnection();
			if (connection.State != System.Data.ConnectionState.Open)
			{
				await connection.OpenAsync(cancellationToken);
			}

			var tableExists = await ExistsAsync(
				connection,
				"SELECT to_regclass('public.\"HR_PayrollRunItems\"') IS NOT NULL;",
				cancellationToken);
			if (!tableExists)
			{
				return HealthCheckResult.Unhealthy(
					"Missing table HR_PayrollRunItems. Run migration 20260428034756_PayrollRunItemSnapshots.");
			}

			var indexExists = await ExistsAsync(
				connection,
				"SELECT to_regclass('public.\"IX_HR_PayrollRunItems_Tenant_Month_Employee\"') IS NOT NULL;",
				cancellationToken);
			if (!indexExists)
			{
				return HealthCheckResult.Unhealthy(
					"Missing index IX_HR_PayrollRunItems_Tenant_Month_Employee.");
			}

			return HealthCheckResult.Healthy("Payroll snapshot schema is ready.");
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy("Payroll snapshot readiness check failed.", ex);
		}
	}

	private static async Task<bool> ExistsAsync(
		DbConnection connection,
		string sql,
		CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = sql;
		var result = await command.ExecuteScalarAsync(cancellationToken);
		return result is bool exists && exists;
	}
}
