using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using ShikshaMitra.Audit;

namespace ShikshaMitra.Finance.Audit;

public static class FinanceAuditTrailProjection
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static FinanceAuditEventDto ToDto(FinanceAuditEvent entity)
	{
		var changedFields = DeserializeChangedFields(entity.ChangedFieldsJson);
		return new FinanceAuditEventDto
		{
			Id = entity.Id,
			AcademicYearId = entity.AcademicYearId,
			SubModuleCode = entity.SubModuleCode,
			SubModuleDisplayName = FinanceAuditSubModuleCodes.ToDisplayName(entity.SubModuleCode),
			EntityType = entity.EntityType,
			EntityId = entity.EntityId,
			EntityName = entity.EntityName,
			Action = entity.Action,
			ActionLabel = ToLabel(entity.Action),
			Reason = entity.Reason,
			Summary = entity.Summary,
			StudentId = entity.StudentId,
			StudentNameSnapshot = entity.StudentNameSnapshot,
			StudentGrNoSnapshot = entity.StudentGrNoSnapshot,
			StudentRollNoSnapshot = entity.StudentRollNoSnapshot,
			ActorUserId = entity.ActorUserId,
			ActorNameSnapshot = entity.ActorNameSnapshot,
			ActorDisplayName = entity.ActorNameSnapshot ?? entity.ActorUserId?.ToString() ?? "System",
			PerformedByRoleSnapshot = entity.PerformedByRoleSnapshot,
			OccurredAt = entity.OccurredAt,
			OccurredAtUtc = AuditTimestampDisplay.NormalizeUtc(entity.OccurredAt),
			OccurredAtDisplay = AuditTimestampDisplay.FormatInstitutionTime(entity.OccurredAt),
			ChangedFields = changedFields,
			ChangedFieldCount = changedFields.Count,
			ContextJson = entity.ContextJson,
			CorrelationId = entity.CorrelationId,
			SourceAuditLogId = entity.SourceAuditLogId
		};
	}

	private static List<FinanceAuditChangedFieldDto> DeserializeChangedFields(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return [];
		}

		try
		{
			return JsonSerializer.Deserialize<List<FinanceAuditChangedFieldDto>>(json, JsonOptions) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	private static string ToLabel(string value) =>
		CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Normalize(value).Replace("_", " "));

	private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace("-", "_");
}
