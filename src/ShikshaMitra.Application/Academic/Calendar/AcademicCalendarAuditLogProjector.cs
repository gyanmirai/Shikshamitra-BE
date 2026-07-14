using System.Collections.Generic;
using System.Text.Json;

namespace ShikshaMitra.Academic.Calendar;

/// <summary>Projects an AcademicCalendarAuditLog entity into the rich FE-facing DTO, parsing the stored diff JSON.</summary>
public static class AcademicCalendarAuditLogProjector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AcademicCalendarAuditLogDto ToDto(AcademicCalendarAuditLog entity)
    {
        var diff = DeserializeDiff(entity.ChangedFieldsJson);
        return new AcademicCalendarAuditLogDto
        {
            Id = entity.Id,
            Module = entity.Module,
            SubModule = entity.SubModule,
            EntityType = entity.EntityType,
            AcademicCalendarId = entity.AcademicCalendarId,
            AcademicCalendarDayId = entity.AcademicCalendarDayId,
            EntityId = entity.AcademicCalendarDayId ?? entity.AcademicCalendarId,
            EntityName = entity.EntityName,

            Action = entity.Action,
            ActionLabel = AcademicCalendarAuditActionLabels.Get(entity.Action),
            Reason = entity.Reason,

            Source = entity.Source,
            ChangedByUserId = entity.ChangedByUserId,
            ChangedByName = entity.ActorNameSnapshot,
            ChangedByRole = entity.ActorRoleSnapshot,
            ChangedByEmail = entity.ActorEmailSnapshot,

            Timestamp = entity.ChangedAt,
            CorrelationId = entity.CorrelationId,
            HasDiff = entity.HasDiff,
            Diff = diff,
            Metadata = new AuditLogMetadataDto
            {
                IpAddress = entity.IpAddress,
                Device = entity.Device,
                Browser = entity.Browser,
                OperatingSystem = entity.OperatingSystem,
                RequestPath = entity.RequestPath,
                HttpMethod = entity.HttpMethod
            }
        };
    }

    private static List<AuditLogDiffDto> DeserializeDiff(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }
        try
        {
            return JsonSerializer.Deserialize<List<AuditLogDiffDto>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
