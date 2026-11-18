using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using Npgsql;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

internal enum NotificationQueueKind { Intent, Dispatch }

internal static class NotificationPostgresQueueClaims
{
	internal static async Task<IReadOnlyList<NotificationQueueClaim>> ClaimAsync(DbConnection connection,
		DbTransaction? transaction, Guid tenantId, NotificationQueueKind kind, DateTimeOffset now,
		int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken)
	{
		if (connection is not NpgsqlConnection) throw new NotSupportedException("Notification queue claims require PostgreSQL.");
		if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
		if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
		if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(10))
			throw new ArgumentOutOfRangeException(nameof(leaseDuration));
		var (table, dueColumn, firstState, secondState, processing, attemptSql) = kind switch
		{
			NotificationQueueKind.Intent => ("NotificationIntents", "DueAt", (int)NotificationIntentState.Ready,
				(int)NotificationIntentState.Scheduled, (int)NotificationIntentState.Processing, ""),
			NotificationQueueKind.Dispatch => ("NotificationDispatches", "NextAttemptAt", (int)NotificationDispatchState.Pending,
				(int)NotificationDispatchState.Pending, (int)NotificationDispatchState.Processing,
				", \"AttemptNumber\" = target.\"AttemptNumber\" + 1"),
			_ => throw new ArgumentOutOfRangeException(nameof(kind))
		};
		var leaseId = Guid.NewGuid();
		var openedHere = connection.State != ConnectionState.Open;
		if (openedHere) await connection.OpenAsync(cancellationToken);
		try
		{
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = $"""
				WITH candidates AS (
				    SELECT "Id" FROM "{table}"
				    WHERE "TenantId" = @tenant AND "IsDeleted" = false
				      AND "Status" IN (@firstState, @secondState) AND "{dueColumn}" <= @now
				      AND ("ExpiresAt" IS NULL OR "ExpiresAt" > @now)
				    ORDER BY "{dueColumn}", "Id" LIMIT @batch FOR UPDATE SKIP LOCKED
				)
				UPDATE "{table}" AS target
				SET "Status" = @processing, "LeaseId" = @lease, "ClaimedAt" = @now,
				    "LeaseExpiresAt" = @expires, "ConcurrencyStamp" = @stamp{attemptSql}
				FROM candidates WHERE target."Id" = candidates."Id" AND target."TenantId" = @tenant
				RETURNING target."Id", target."TenantId", target."LeaseId"
				""";
			Parameter(command, "tenant", tenantId);
			Parameter(command, "firstState", firstState);
			Parameter(command, "secondState", secondState);
			Parameter(command, "processing", processing);
			Parameter(command, "now", now.ToUniversalTime());
			Parameter(command, "batch", batchSize);
			Parameter(command, "lease", leaseId);
			Parameter(command, "expires", now.ToUniversalTime().Add(leaseDuration));
			Parameter(command, "stamp", leaseId.ToString("N"));
			var claims = new List<NotificationQueueClaim>();
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			while (await reader.ReadAsync(cancellationToken))
				claims.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2)));
			return claims.AsReadOnly();
		}
		finally
		{
			if (openedHere) await connection.CloseAsync();
		}
	}

	private static void Parameter(DbCommand command, string name, object value)
	{
		var parameter = command.CreateParameter();
		parameter.ParameterName = name;
		parameter.Value = value;
		command.Parameters.Add(parameter);
	}
}
