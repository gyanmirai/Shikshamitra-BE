using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Audit;

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
		var changedFields = DeserializeChangedFields(changedFieldsJson);

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
			ContextJson = contextJson,
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
			return JsonSerializer.Deserialize<List<AcademicAuditChangedFieldDto>>(json, JsonOptions) ?? [];
		}
		catch (JsonException)
		{
			try
			{
				var calendarDiff = JsonSerializer.Deserialize<List<CalendarAuditDiffShape>>(json, JsonOptions) ?? [];
				return calendarDiff.ConvertAll(x => new AcademicAuditChangedFieldDto
				{
					Field = x.FieldName,
					DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? ToLabel(x.FieldName) : x.DisplayName,
					OldValue = x.OldValue,
					NewValue = x.NewValue,
					ChangeType = string.IsNullOrWhiteSpace(x.ChangeType) ? "updated" : x.ChangeType
				});
			}
			catch (JsonException)
			{
				return [];
			}
		}
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
		CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Normalize(value).Replace("_", " "));

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
