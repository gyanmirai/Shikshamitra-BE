using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ShikshaMitra.Academic.Calendar;

/// <summary>Projects an AcademicCalendarAuditLog entity into the rich FE-facing DTO, parsing the stored diff JSON.</summary>
public static class AcademicCalendarAuditLogProjector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AcademicCalendarAuditLogDto ToDto(AcademicCalendarAuditLog entity)
    {
        var diff = DeserializeDiff(entity.ChangedFieldsJson)
            .Where(x => !AreEquivalentCalendarDates(x))
            .ToList();
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
            HasDiff = entity.HasDiff && diff.Count > 0,
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

    // Keep historical audit records clean too: older entries may contain a date-only change
    // caused solely by one snapshot including a timezone offset.
    private static bool AreEquivalentCalendarDates(AuditLogDiffDto field)
    {
        if (!field.FieldName.EndsWith("Date", StringComparison.OrdinalIgnoreCase)
            || field.OldValue == null
            || field.NewValue == null)
        {
            return false;
        }

        return TryGetCalendarDate(field.OldValue, out var oldDate)
            && TryGetCalendarDate(field.NewValue, out var newDate)
            && oldDate == newDate;
    }

    private static bool TryGetCalendarDate(string value, out DateOnly date)
    {
        date = default;
        return value.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", out date);
    }
}
