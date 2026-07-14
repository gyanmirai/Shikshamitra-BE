using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarAuditLog : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicCalendarId { get; private set; }
    public Guid? AcademicCalendarDayId { get; private set; }

    public CalendarAuditAction Action { get; private set; }
    public string OldValueJson { get; private set; }
    public string NewValueJson { get; private set; }
    public string? Reason { get; private set; }

    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAt { get; private set; }

    /// <summary>Discriminator so Emergency Closure / Impact Log rows sharing this table can be filtered out.</summary>
    public string Module { get; private set; } = "CalendarManagement";
    public string? SubModule { get; private set; }

    public string? EntityType { get; private set; }
    public string? EntityName { get; private set; }

    /// <summary>Structured field-level diff (JSON array of {field, oldValue, newValue}) — preferred over the raw OldValueJson/NewValueJson dumps.</summary>
    public string? ChangedFieldsJson { get; private set; }
    public bool HasDiff { get; private set; }

    public string? ActorNameSnapshot { get; private set; }
    public string? ActorRoleSnapshot { get; private set; }
    public string? ActorEmailSnapshot { get; private set; }

    public CalendarAuditSource? Source { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Device { get; private set; }
    public string? Browser { get; private set; }
    public string? OperatingSystem { get; private set; }
    public string? RequestPath { get; private set; }
    public string? HttpMethod { get; private set; }
    public string? CorrelationId { get; private set; }

    private AcademicCalendarAuditLog()
    {
        /* ORM */
    }

    public AcademicCalendarAuditLog(
        Guid id,
        Guid? tenantId,
        Guid academicCalendarId,
        CalendarAuditAction action,
        string oldValueJson,
        string newValueJson,
        Guid changedByUserId,
        string? reason = null,
        Guid? academicCalendarDayId = null,
        string module = "CalendarManagement",
        string? subModule = null,
        string? entityType = null,
        string? entityName = null,
        string? changedFieldsJson = null,
        string? actorNameSnapshot = null,
        string? actorRoleSnapshot = null,
        string? actorEmailSnapshot = null,
        CalendarAuditSource? source = null,
        string? ipAddress = null,
        string? device = null,
        string? browser = null,
        string? operatingSystem = null,
        string? requestPath = null,
        string? httpMethod = null,
        string? correlationId = null)
        : base(id)
    {
        TenantId = tenantId;
        AcademicCalendarId = academicCalendarId;
        Action = action;
        OldValueJson = oldValueJson;
        NewValueJson = newValueJson;
        ChangedByUserId = changedByUserId;
        Reason = reason;
        AcademicCalendarDayId = academicCalendarDayId;
        ChangedAt = DateTime.UtcNow;

        Module = module;
        SubModule = subModule;
        EntityType = entityType;
        EntityName = entityName;
        ChangedFieldsJson = changedFieldsJson;
        HasDiff = !string.IsNullOrWhiteSpace(changedFieldsJson) && changedFieldsJson != "[]";
        ActorNameSnapshot = actorNameSnapshot;
        ActorRoleSnapshot = actorRoleSnapshot;
        ActorEmailSnapshot = actorEmailSnapshot;
        Source = source;
        IpAddress = ipAddress;
        Device = device;
        Browser = browser;
        OperatingSystem = operatingSystem;
        RequestPath = requestPath;
        HttpMethod = httpMethod;
        CorrelationId = correlationId;
    }
}
