using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ShikshaMitra.HR.Audit;

public static class HrAuditTrailProjection
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static HrAuditEventDto ToDto(HrAuditEvent entity)
	{
		var changedFields = DeserializeChangedFields(entity.ChangedFieldsJson);
		return new()
		{
			Id = entity.Id,
			AcademicYearId = entity.AcademicYearId,
			EntityType = entity.EntityType,
			EntityId = entity.EntityId,
			EntityName = entity.EntityName,
			Action = entity.Action,
			ActorUserId = entity.ActorUserId,
			ActorNameSnapshot = entity.ActorNameSnapshot,
			OccurredAt = entity.OccurredAt,
			OccurredAtUtc = NormalizeUtc(entity.OccurredAt),
			OccurredAtDisplay = string.Empty,
			ActorDisplayName = entity.ActorNameSnapshot ?? entity.ActorUserId?.ToString() ?? "System",
			ActionLabel = ToLabel(entity.Action),
			Reason = entity.Reason,
			Summary = entity.Summary,
			ChangedFields = changedFields,
			ChangedFieldCount = changedFields.Count,
			ContextJson = entity.ContextJson,
			CorrelationId = entity.CorrelationId,
			SourceAuditLogId = entity.SourceAuditLogId
		};
	}

	private static List<HrAuditChangedFieldDto> DeserializeChangedFields(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return [];
		}
		try
		{
			return JsonSerializer.Deserialize<List<HrAuditChangedFieldDto>>(json, JsonOptions) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
	{
		DateTimeKind.Utc => value,
		DateTimeKind.Local => value.ToUniversalTime(),
		_ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
	};

	private static string ToLabel(string value) =>
		CultureInfo.InvariantCulture.TextInfo.ToTitleCase(HrAuditTrailQuery.Normalize(value).Replace("_", " "));
}
