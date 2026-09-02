using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Audit;
using ShikshaMitra.Academic.Calendar;

namespace ShikshaMitra.Academic.Audit;

public static class AcademicAuditTrailProjection
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static AcademicAuditEventDto Create(
		string source,
		string subModuleCode,
		string entityType,
		Guid entityId,
		string entityName,
		string action,
		DateTime occurredAt,
		string summary,
		Guid? sourceAuditLogId = null,
		Guid? academicYearId = null,
		Guid? actorUserId = null,
		string? actorName = null,
		string? actorRole = null,
		string? reason = null,
		string? changedFieldsJson = null,
		string? contextJson = null,
		string? correlationId = null)
	{
		var normalizedAction = Normalize(action);
		var normalizedSubModule = Normalize(subModuleCode);
		var normalizedEntityType = Normalize(entityType);
		var changedFields = DeserializeChangedFields(changedFieldsJson)
			.Where(x => !AreEquivalentDateValues(x))
			.ToList();

		return new AcademicAuditEventDto
		{
			Id = sourceAuditLogId ?? CreateStableId($"{source}:{entityType}:{entityId}:{normalizedAction}:{occurredAt:O}"),
			AcademicYearId = academicYearId,
			SubModuleCode = normalizedSubModule,
			SubModuleDisplayName = ToSubModuleDisplayName(normalizedSubModule),
			EntityType = normalizedEntityType,
			EntityId = entityId,
			EntityName = string.IsNullOrWhiteSpace(entityName) ? normalizedEntityType : entityName,
			Action = normalizedAction,
			ActionLabel = ToLabel(normalizedAction),
			Summary = summary,
			Reason = reason,
			ActorUserId = actorUserId,
			ActorNameSnapshot = actorName,
			ActorDisplayName = actorName ?? actorUserId?.ToString() ?? "System",
			ActorRoleSnapshot = actorRole,
			OccurredAt = occurredAt,
			OccurredAtUtc = AuditTimestampDisplay.NormalizeUtc(occurredAt),
			OccurredAtDisplay = AuditTimestampDisplay.FormatInstitutionTime(occurredAt),
			ChangedFields = changedFields,
			ChangedFieldCount = changedFields.Count,
			// The event already contains the human-readable entity and field changes. Do not return
			// raw before/after snapshots here: they expose internal GUID references to the UI.
			ContextJson = ExtractMetadataContext(contextJson),
			CorrelationId = correlationId,
			SourceAuditLogId = sourceAuditLogId,
			Source = source
		};
	}

	private static List<AcademicAuditChangedFieldDto> DeserializeChangedFields(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return [];

		try
		{
			var direct = JsonSerializer.Deserialize<List<AcademicAuditChangedFieldDto>>(json, JsonOptions) ?? [];
			if (direct.Count == 0 || direct.Any(x => string.IsNullOrWhiteSpace(x.Field)))
			{
				var calendarDiff = JsonSerializer.Deserialize<List<CalendarAuditDiffShape>>(json, JsonOptions) ?? [];
				if (calendarDiff.Count > 0)
					return calendarDiff.ConvertAll(MapCalendarDiff);
			}
			return direct;
		}
		catch (JsonException)
		{
			try
			{
				var calendarDiff = JsonSerializer.Deserialize<List<CalendarAuditDiffShape>>(json, JsonOptions) ?? [];
				return calendarDiff.ConvertAll(MapCalendarDiff);
			}
			catch (JsonException)
			{
				return [];
			}
		}
	}

	private static AcademicAuditChangedFieldDto MapCalendarDiff(CalendarAuditDiffShape x) => new()
	{
		Field = x.FieldName,
		DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? ToLabel(x.FieldName) : x.DisplayName,
		OldValue = FormatCalendarValue(x.FieldName, x.OldValue),
		NewValue = FormatCalendarValue(x.FieldName, x.NewValue),
		ChangeType = string.IsNullOrWhiteSpace(x.ChangeType) ? "updated" : x.ChangeType
	};

	private static bool AreEquivalentDateValues(AcademicAuditChangedFieldDto field) =>
		field.Field.EndsWith("Date", StringComparison.OrdinalIgnoreCase)
		&& TryParseDate(field.OldValue, out var oldDate)
		&& TryParseDate(field.NewValue, out var newDate)
		&& oldDate == newDate;

	private static string? FormatCalendarValue(string fieldName, string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return value;
		if (fieldName.Equals("HolidayCategory", StringComparison.OrdinalIgnoreCase)
			&& int.TryParse(value, out var category)
			&& Enum.IsDefined(typeof(HolidayCategory), category))
			return ((HolidayCategory)category).ToString();
		return fieldName.EndsWith("Date", StringComparison.OrdinalIgnoreCase) && TryParseDate(value, out var date)
			? date.ToString("dd-MM-yyyy")
			: value;
	}

	private static bool TryParseDate(string? value, out DateOnly date)
	{
		date = default;
		var normalized = value?.Trim('"');
		return normalized?.Length >= 10 && DateOnly.TryParseExact(normalized[..10], "yyyy-MM-dd", out date);
	}

	private static string? ExtractMetadataContext(string? contextJson)
	{
		if (string.IsNullOrWhiteSpace(contextJson)) return null;
		try
		{
			using var document = JsonDocument.Parse(contextJson);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
				return null;

			return document.RootElement.TryGetProperty("metadata", out var metadata)
				? metadata.GetRawText()
				: null;
		}
		catch (JsonException) { return null; }
	}

	private static string ToSubModuleDisplayName(string value) => value switch
	{
		"academic_calendar" => "Academic Calendar",
		"subject_creation" => "Subject Creation",
		"elective_group_creation" => "Elective Group Creation",
		"room_availability" => "Room Availability",
		"substitution_management" => "Substitution Management",
		"seating_plan" => "Seating Plan",
		"result_creation" => "Result Creation",
		_ => ToLabel(value)
	};

	private static string ToLabel(string value) =>
		CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
			System.Text.RegularExpressions.Regex.Replace(value.Trim(), "([a-z])([A-Z])", "$1 $2")
				.Replace("_", " ").Replace("-", " ").ToLowerInvariant());

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");

	private static Guid CreateStableId(string value)
	{
		var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
		return new Guid(hash);
	}

	private sealed class CalendarAuditDiffShape
	{
		public string FieldName { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string? OldValue { get; set; }
		public string? NewValue { get; set; }
		public string ChangeType { get; set; } = "updated";
	}
}
