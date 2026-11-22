using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

internal static class NotificationPostgresPersonalLocks
{
	internal static async Task AcquireAsync(DbConnection connection, DbTransaction? transaction, Guid tenantId,
		IReadOnlyList<string> keys, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (connection is not NpgsqlConnection) throw new NotSupportedException("Personal notification write locks require PostgreSQL.");
		if (transaction == null || transaction.Connection != connection) throw new InvalidOperationException("Personal notification writes require a transaction.");
		if (tenantId == Guid.Empty || keys.Count is < 1 or > 4 || keys.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 512))
			throw new ArgumentException("A tenant and bounded write keys are required.");
		foreach (var scope in keys.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
		{
			var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"notification-personal/{tenantId:N}/{scope}"));
			await using var command = connection.CreateCommand(); command.Transaction = transaction;
			command.CommandText = "SELECT pg_advisory_xact_lock(@key)";
			var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = BinaryPrimitives.ReadInt64BigEndian(hash);
			command.Parameters.Add(parameter); await command.ExecuteNonQueryAsync(cancellationToken);
		}
	}
}
