using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

internal static class NotificationPostgresQueueHealth
{
	private const string SnapshotSql = """
		SELECT COUNT(*), MIN("DueAt")
		FROM "NotificationIntents"
		WHERE "TenantId" = @tenant AND NOT "IsDeleted"
		  AND "Status" IN (@intentReady, @intentScheduled) AND "DueAt" <= @now
		  AND ("ExpiresAt" IS NULL OR "ExpiresAt" > @now);

		SELECT "Channel",
		       COUNT(*) FILTER (WHERE "Status" = @pending),
		       COUNT(*) FILTER (WHERE "Status" = @pending AND "NextAttemptAt" <= @now),
		       MIN("NextAttemptAt") FILTER (WHERE "Status" = @pending AND "NextAttemptAt" <= @now),
		       COUNT(*) FILTER (WHERE "Status" = @processing),
		       COUNT(*) FILTER (WHERE "Status" = @unknown),
		       COUNT(*) FILTER (WHERE "Status" = @failed),
		       COUNT(*) FILTER (WHERE "Status" = @pending AND "AttemptNumber" > 0),
		       COUNT(*) FILTER (WHERE "Status" = @pending AND "ErrorCode" IN (@whatsAppLimited, @webPushLimited, @quotaExhausted)),
		       COUNT(*) FILTER (WHERE "Status" = @failed AND "ErrorCode" = @subscriptionExpired),
		       COUNT(*) FILTER (WHERE "Status" = @skipped AND "ErrorCode" = @consentRequired),
		       COUNT(*) FILTER (WHERE "Status" = @skipped AND "ErrorCode" = ANY(@setupReasons))
		FROM "NotificationDispatches"
		WHERE "TenantId" = @tenant AND NOT "IsDeleted"
		  AND (("Status" = @pending AND ("ExpiresAt" IS NULL OR "ExpiresAt" > @now))
		       OR "Status" IN (@processing, @unknown)
		       OR (COALESCE("LastModificationTime", "CreationTime") >= @recentFrom AND
		           ("Status" = @failed OR ("Status" = @skipped AND
		            ("ErrorCode" = @consentRequired OR "ErrorCode" = ANY(@setupReasons))))))
		GROUP BY "Channel"
		ORDER BY "Channel";

		SELECT COUNT(*), MIN("ReceivedAt")
		FROM "NotificationProviderEvents"
		WHERE "TenantId" = @tenant AND NOT "IsDeleted" AND "ProcessedAt" IS NULL
		  AND "ProviderEventKey" LIKE 'wa:%' AND "ProviderMessageId" IS NOT NULL;
		""";

	internal static async Task<NotificationQueueHealth> ReadAsync(DbConnection connection, Guid tenantId,
		DateTimeOffset now, CancellationToken cancellationToken)
	{
		if (connection is not NpgsqlConnection postgres) throw new NotSupportedException("Notification queue health requires PostgreSQL.");
		if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
		var utc = now.ToUniversalTime();
		var openedHere = connection.State != ConnectionState.Open;
		if (openedHere) await connection.OpenAsync(cancellationToken);
		try
		{
			await using var command = new NpgsqlCommand(SnapshotSql, postgres) { CommandTimeout = 10 };
			command.Parameters.AddWithValue("tenant", tenantId);
			command.Parameters.AddWithValue("now", utc);
			command.Parameters.AddWithValue("recentFrom", utc.AddHours(-1).UtcDateTime);
			command.Parameters.AddWithValue("intentReady", (int)NotificationIntentState.Ready);
			command.Parameters.AddWithValue("intentScheduled", (int)NotificationIntentState.Scheduled);
			command.Parameters.AddWithValue("pending", (int)NotificationDispatchState.Pending);
			command.Parameters.AddWithValue("processing", (int)NotificationDispatchState.Processing);
			command.Parameters.AddWithValue("unknown", (int)NotificationDispatchState.UnknownAcceptance);
			command.Parameters.AddWithValue("failed", (int)NotificationDispatchState.Failed);
			command.Parameters.AddWithValue("skipped", (int)NotificationDispatchState.Skipped);
			command.Parameters.AddWithValue("whatsAppLimited", "WhatsAppRateLimited");
			command.Parameters.AddWithValue("webPushLimited", "WebPushRateLimited");
			command.Parameters.AddWithValue("quotaExhausted", NotificationErrorCodes.ProviderQuotaExhausted);
			command.Parameters.AddWithValue("subscriptionExpired", "WebPushSubscriptionExpired");
			command.Parameters.AddWithValue("consentRequired", "ConsentRequired");
			command.Parameters.AddWithValue("setupReasons", new[] { NotificationErrorCodes.ProviderUnavailable,
				"WhatsAppTemplateNotApproved", "WebPushSubscriptionUnavailable", "VerifiedAccountRequired" });
			await using var reader = await command.ExecuteReaderAsync(cancellationToken);
			await reader.ReadAsync(cancellationToken);
			var dueIntents = reader.GetInt64(0);
			var oldestDueIntent = DateAt(reader, 1);

			await reader.NextResultAsync(cancellationToken);
			var channels = new List<NotificationChannelQueueHealth>();
			while (await reader.ReadAsync(cancellationToken))
				channels.Add(new((CommunicationChannelType)reader.GetInt32(0), reader.GetInt64(1), reader.GetInt64(2),
					DateAt(reader, 3), reader.GetInt64(4), reader.GetInt64(5), reader.GetInt64(6),
					reader.GetInt64(7), reader.GetInt64(8), reader.GetInt64(9), reader.GetInt64(10), reader.GetInt64(11)));

			await reader.NextResultAsync(cancellationToken);
			await reader.ReadAsync(cancellationToken);
			return new(dueIntents, oldestDueIntent, channels.AsReadOnly(), reader.GetInt64(0), DateAt(reader, 1));
		}
		finally
		{
			if (openedHere) await connection.CloseAsync();
		}
	}

	private static DateTimeOffset? DateAt(DbDataReader reader, int index) =>
		reader.IsDBNull(index) ? null : reader.GetFieldValue<DateTimeOffset>(index);
}
