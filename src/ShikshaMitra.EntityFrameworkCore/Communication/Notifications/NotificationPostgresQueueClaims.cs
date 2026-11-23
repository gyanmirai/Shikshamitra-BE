using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication;
using ShikshaMitra.Communication.Notifications;
using Npgsql;

namespace ShikshaMitra.EntityFrameworkCore.Communication.Notifications;

internal enum NotificationQueueKind { Intent, Dispatch }

internal static class NotificationPostgresQueueClaims
{
	private const string PilotDispatchFilter = """
		AND candidate."CampaignId" = @pilotOperation AND candidate."Channel" = @pilotChannel
		AND candidate."AttemptNumber" = 0 AND candidate."ExpiresAt" IS NOT NULL
		AND (candidate."ProviderSettingId" IS NOT NULL) = @requiresProviderSetting
		AND EXISTS (
		    SELECT 1 FROM "NotificationIntents" AS intent
		    WHERE intent."Id" = candidate."IntentId" AND intent."TenantId" = @tenant
		      AND intent."CampaignId" = @pilotOperation AND intent."IsDeleted" = false
		      AND intent."SourceType" = @templateTestSource
		)
		AND EXISTS (
		    SELECT 1 FROM "CommunicationRecipientSnapshots" AS recipient
		    WHERE recipient."Id" = candidate."RecipientSnapshotId" AND recipient."TenantId" = @tenant
		      AND recipient."CampaignId" = @pilotOperation AND recipient."IsDeleted" = false
		      AND recipient."UserId" = @pilotRecipient AND recipient."RecipientType" = 'TestAccount'
		      AND recipient."IsEligible" = true
		)
		AND NOT EXISTS (
		    SELECT 1 FROM "NotificationDispatches" AS sibling
		    WHERE sibling."TenantId" = @tenant AND sibling."CampaignId" = @pilotOperation
		      AND sibling."Id" <> candidate."Id"
		)
		""";

	internal static async Task<IReadOnlyList<NotificationQueueClaim>> ClaimAsync(DbConnection connection,
		DbTransaction? transaction, Guid tenantId, NotificationQueueKind kind, DateTimeOffset now,
		int batchSize, TimeSpan leaseDuration, CancellationToken cancellationToken, int? maxActivePerChannel = null,
		IReadOnlyList<CommunicationChannelType>? pausedChannels = null, NotificationTemplateTestPilot? pilot = null)
	{
		if (connection is not NpgsqlConnection) throw new NotSupportedException("Notification queue claims require PostgreSQL.");
		if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
		if (batchSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(batchSize));
		if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(10))
			throw new ArgumentOutOfRangeException(nameof(leaseDuration));
		if (maxActivePerChannel.HasValue && (kind != NotificationQueueKind.Dispatch || batchSize != 1 || maxActivePerChannel is < 1 or > 100))
			throw new ArgumentOutOfRangeException(nameof(maxActivePerChannel));
		if (maxActivePerChannel.HasValue && transaction?.IsolationLevel != IsolationLevel.ReadCommitted)
			throw new InvalidOperationException("Notification channel capacity requires a read committed transaction.");
		var filterPaused = pausedChannels is { Count: > 0 };
		if (filterPaused && kind != NotificationQueueKind.Dispatch) throw new ArgumentException("Only dispatches have channels.", nameof(pausedChannels));
		if (pilot != null && (kind != NotificationQueueKind.Dispatch || batchSize != 1 || pilot.TenantId != tenantId ||
			pilot.OperationId == Guid.Empty || pilot.RecipientUserId == Guid.Empty || !pilot.HasSupportedChannel))
			throw new ArgumentException("The template test pilot must claim one dispatch for its designated tenant and channel.", nameof(pilot));
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
			if (maxActivePerChannel.HasValue)
				await NotificationPostgresPersonalLocks.AcquireAsync(connection, transaction, tenantId,
					["notification-dispatch-claim"], cancellationToken);
			var capacity = maxActivePerChannel.HasValue ? """
				AND (SELECT COUNT(*) FROM "NotificationDispatches" AS active
				     WHERE active."TenantId" = @tenant AND active."Channel" = candidate."Channel"
				       AND active."Status" = @processing AND active."IsDeleted" = false AND active."LeaseExpiresAt" > @now) < @maxActive
				""" : "";
			var pauseFilter = filterPaused ? """AND candidate."Channel" <> ALL (@pausedChannels)""" : "";
			var pilotFilter = pilot != null ? PilotDispatchFilter : "";
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = $"""
				WITH candidates AS (
				    SELECT "Id" FROM "{table}" AS candidate
				    WHERE "TenantId" = @tenant AND "IsDeleted" = false
				      AND "Status" IN (@firstState, @secondState) AND "{dueColumn}" <= @now
				      AND ("ExpiresAt" IS NULL OR "ExpiresAt" > @now)
				      {pauseFilter}
				      {pilotFilter}
				      {capacity}
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
			if (maxActivePerChannel.HasValue) Parameter(command, "maxActive", maxActivePerChannel.Value);
			if (filterPaused) Parameter(command, "pausedChannels", pausedChannels!.Select(channel => (int)channel).ToArray());
			if (pilot != null)
			{
				Parameter(command, "pilotOperation", pilot.OperationId);
				Parameter(command, "pilotRecipient", pilot.RecipientUserId);
				Parameter(command, "pilotChannel", (int)pilot.Channel);
				Parameter(command, "requiresProviderSetting", pilot.Channel == CommunicationChannelType.Email);
				Parameter(command, "templateTestSource", NotificationCampaignTypes.AcademicTemplateTestSource);
			}
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

	internal static async Task<bool> RenewDispatchAsync(DbConnection connection, DbTransaction? transaction, Guid tenantId,
		Guid dispatchId, Guid leaseId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken)
	{
		if (connection is not NpgsqlConnection) throw new NotSupportedException("Notification queue leases require PostgreSQL.");
		if (tenantId == Guid.Empty || dispatchId == Guid.Empty || leaseId == Guid.Empty) throw new ArgumentException("A tenant, dispatch and lease are required.");
		if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(10))
			throw new ArgumentOutOfRangeException(nameof(leaseDuration));
		var openedHere = connection.State != ConnectionState.Open;
		if (openedHere) await connection.OpenAsync(cancellationToken);
		try
		{
			await using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = """
				UPDATE "NotificationDispatches"
				SET "LeaseExpiresAt" = GREATEST("LeaseExpiresAt", @expires), "ConcurrencyStamp" = @stamp
				WHERE "Id" = @dispatch AND "TenantId" = @tenant AND "IsDeleted" = false AND "Status" = @processing
				  AND "LeaseId" = @lease AND "LeaseExpiresAt" > @now AND ("ExpiresAt" IS NULL OR "ExpiresAt" > @now)
				""";
			Parameter(command, "tenant", tenantId);
			Parameter(command, "dispatch", dispatchId);
			Parameter(command, "lease", leaseId);
			Parameter(command, "processing", (int)NotificationDispatchState.Processing);
			Parameter(command, "now", now.ToUniversalTime());
			Parameter(command, "expires", now.ToUniversalTime().Add(leaseDuration));
			Parameter(command, "stamp", Guid.NewGuid().ToString("N"));
			return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
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
