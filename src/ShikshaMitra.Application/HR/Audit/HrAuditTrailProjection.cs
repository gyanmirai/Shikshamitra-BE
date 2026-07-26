using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ShikshaMitra.Audit;

namespace ShikshaMitra.HR.Audit;

public static class HrAuditTrailProjection
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly HashSet<string> SemanticDateFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"effectiveFrom",
		"effectiveTo"
	};
	private static readonly HashSet<string> SemanticTimestampFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"timeIn",
		"timeOut"
	};

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
			OccurredAtUtc = AuditTimestampDisplay.NormalizeUtc(entity.OccurredAt),
			OccurredAtDisplay = AuditTimestampDisplay.FormatInstitutionTime(entity.OccurredAt),
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
			return (JsonSerializer.Deserialize<List<HrAuditChangedFieldDto>>(json, JsonOptions) ?? [])
				.Where(change => !AreEquivalent(change.Field, change.OldValue, change.NewValue))
				.ToList();
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static bool AreEquivalent(string field, string? oldValue, string? newValue)
	{
		var left = oldValue?.Trim().Trim('"');
		var right = newValue?.Trim().Trim('"');
		if (string.Equals(left, right, StringComparison.Ordinal))
		{
			return true;
		}

		if (SemanticDateFields.Contains(field))
		{
			return TryParseCalendarDate(left, out var leftDate) &&
				TryParseCalendarDate(right, out var rightDate) &&
				leftDate == rightDate;
		}

		return SemanticTimestampFields.Contains(field) &&
			DateTimeOffset.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var leftTimestamp) &&
			DateTimeOffset.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rightTimestamp) &&
			leftTimestamp.Equals(rightTimestamp);
	}

	private static bool TryParseCalendarDate(string? value, out DateOnly date)
	{
		date = default;
		return DateTimeOffset.TryParse(
				value,
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal,
				out var timestamp) &&
			(date = DateOnly.FromDateTime(timestamp.DateTime)) != default;
	}

	private static string ToLabel(string value) =>
		CultureInfo.InvariantCulture.TextInfo.ToTitleCase(HrAuditTrailQuery.Normalize(value).Replace("_", " "));
}
